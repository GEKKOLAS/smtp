using System.Text;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Templates;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Sending;

/// <summary>
/// Processes one send job: render per recipient → build the provider-agnostic
/// message → send → record the outcome, with per-recipient retry/backoff and
/// job finalization (spec 07 §4, 10-jobs.md J1).
/// </summary>
internal sealed class EmailSendService(
    IAppDbContext db,
    ITemplateRenderer renderer,
    ITokenRefreshService tokenRefresh,
    IEmailProviderClientFactory providerClients,
    IObjectStorage storage,
    IBackgroundJobScheduler scheduler,
    IAuditWriter audit,
    IOptions<StorageOptions> storageOptions,
    IOptions<SendLimitsOptions> limitsOptions,
    IClock clock,
    ILogger<EmailSendService> logger) : IEmailSendService
{
    public async Task ProcessJobAsync(Guid sendJobId, CancellationToken ct)
    {
        var job = await db.EmailSendJobs
            .IgnoreQueryFilters()
            .Include(j => j.Recipients)
            .Include(j => j.Attachments)
            .Include(j => j.Account)
            .Include(j => j.TemplateVersion!).ThenInclude(v => v.TemplateAssets).ThenInclude(ta => ta.Asset)
            .FirstOrDefaultAsync(j => j.Id == sendJobId, ct);

        if (job is null || job.Status is SendJobStatus.Sent or SendJobStatus.Cancelled) return;
        if (job.Account is null || job.TemplateVersion is null) return;

        job.MarkSending(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        var limits = limitsOptions.Value;
        var content = TemplateContentMapping.ToRenderContent(job.TemplateVersion);
        var jobVars = Deserialize(job.VariableValues);

        SendAssets? assets = null;
        try
        {
            var providerClient = providerClients.For(job.Account.Provider);
            var now = clock.UtcNow;

            foreach (var recipient in job.Recipients.Where(r => IsDue(r, now)))
            {
                using var scope = logger.BeginScope(new Dictionary<string, object> { ["RecipientId"] = recipient.Id });
                recipient.Status = RecipientStatus.Sending;

                try
                {
                    assets ??= await LoadAssetsAsync(job, ct);
                    var accountContext = await tokenRefresh.GetValidContextAsync(job.ConnectedEmailAccountId, ct);
                    var email = BuildEmail(job, recipient, content, jobVars, assets, accountContext);

                    var result = await providerClient.SendAsync(accountContext, email, ct);
                    recipient.MarkSent(result.ProviderMessageId, result.ThreadId, clock.UtcNow);
                    RecordEvent(job, ProviderEventTypes.SendSuccess, null, null);
                }
                catch (ProviderSendException ex)
                {
                    HandleSendfailure(job, recipient, ex, limits);
                    if (ex.Kind is ProviderErrorKind.AuthRevoked or ProviderErrorKind.InsufficientScope)
                    {
                        job.Account.MarkNeedsReconnect(AccountStateReasons.InvalidGrant);
                        job.FailAll(ex.Kind.ToString(), ex.SafeMessage, clock.UtcNow);
                        await FinalizeAsync(job, ct);
                        return;
                    }
                }
                catch (RefreshTokenRevokedException)
                {
                    job.FailAll("auth_revoked", "The sending account needs to be reconnected.", clock.UtcNow);
                    await FinalizeAsync(job, ct);
                    return;
                }

                await db.SaveChangesAsync(ct);
                await Task.Delay(providerClient.MinSendInterval, ct);
            }

            await PersistSnapshotAsync(job, content, jobVars, assets, ct);
            await FinalizeAsync(job, ct);
        }
        finally
        {
            assets?.Dispose();
        }
    }

    private void HandleSendfailure(EmailSendJob job, EmailSendRecipient recipient, ProviderSendException ex, SendLimitsOptions limits)
    {
        var now = clock.UtcNow;
        RecordEvent(job, ProviderEventTypes.SendFailure, ex.Kind.ToString(), (int?)null);

        switch (ex.Kind)
        {
            case ProviderErrorKind.Transient or ProviderErrorKind.QuotaExceeded or ProviderErrorKind.AuthExpired:
                var delay = ex.RetryAfter ?? Backoff(recipient.AttemptCount);
                recipient.ScheduleRetry(delay, limits.MaxAttemptsPerRecipient, ex.Kind.ToString(), ex.SafeMessage, now);
                break;
            default:
                recipient.MarkFailed(ex.Kind.ToString(), ex.SafeMessage, now);
                break;
        }
    }

    private async Task FinalizeAsync(EmailSendJob job, CancellationToken ct)
    {
        job.Finalize(clock.UtcNow);

        if (job.Status == SendJobStatus.Retrying && job.NextAttemptAt is { } next)
        {
            var delay = next - clock.UtcNow;
            scheduler.ScheduleSendRetry(job.Id, delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(5));
        }
        else if (job.Status is SendJobStatus.Sent or SendJobStatus.PartiallyFailed)
        {
            audit.Add(AuditActions.SendCompleted, job.UserId, "email_send_job", job.Id);
        }
        else if (job.Status == SendJobStatus.Failed)
        {
            audit.Add(AuditActions.SendFailed, job.UserId, "email_send_job", job.Id);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Send job {JobId} finalized as {Status}", job.Id, job.Status);
    }

    private OutgoingEmail BuildEmail(
        EmailSendJob job, EmailSendRecipient recipient, TemplateContent content,
        IReadOnlyDictionary<string, string?> jobVars, SendAssets assets, ConnectedAccountContext account)
    {
        var vars = new Dictionary<string, string?>(jobVars, StringComparer.Ordinal);
        foreach (var (key, value) in Deserialize(recipient.VariableOverrides)) vars[key] = value;

        var rendered = renderer.Render(new RenderRequest(content, vars, Strict: false, assets.Urls));

        var headers = new Dictionary<string, string> { ["X-MailTemplateHub-Ref"] = recipient.Id.ToString() };
        return new OutgoingEmail(
            new Application.Abstractions.Email.MailboxAddress(account.EmailAddress, null),
            [new Application.Abstractions.Email.MailboxAddress(recipient.EmailAddress, recipient.DisplayName)],
            job.SubjectSnapshot,
            rendered.Html,
            rendered.Text,
            assets.Inline,
            assets.Attachments,
            headers);
    }

    private async Task<SendAssets> LoadAssetsAsync(EmailSendJob job, CancellationToken ct)
    {
        var bucket = storageOptions.Value.PrivateBucket;
        var urls = new Dictionary<Guid, string>();
        var inline = new List<CidAttachment>();
        var attachments = new List<FileAttachment>();

        foreach (var ta in job.TemplateVersion!.TemplateAssets)
        {
            if (ta.Asset is null) continue;
            switch (ta.Usage)
            {
                case TemplateAssetUsage.HostedImage when ta.Asset.PublicUrl is { } publicUrl:
                    urls[ta.AssetId] = publicUrl;
                    break;
                case TemplateAssetUsage.InlineCid when ta.ContentId is { } contentId:
                    var bytes = await DownloadAsync(bucket, ta.Asset.StorageKey, ct);
                    urls[ta.AssetId] = $"cid:{contentId}";
                    inline.Add(new CidAttachment(contentId, ta.Asset.OriginalFilename, ta.Asset.MimeType, bytes));
                    break;
            }
        }

        foreach (var attachment in job.Attachments)
        {
            var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == attachment.AssetId, ct);
            if (asset is null) continue;
            var bytes = await DownloadAsync(bucket, asset.StorageKey, ct);
            var name = attachment.FilenameOverride ?? asset.OriginalFilename;
            if (attachment.Disposition == SendAttachmentDisposition.Inline && attachment.ContentId is { } cid)
            {
                inline.Add(new CidAttachment(cid, name, asset.MimeType, bytes));
            }
            else
            {
                attachments.Add(new FileAttachment(name, asset.MimeType, bytes));
            }
        }

        return new SendAssets(urls, inline, attachments);
    }

    private async Task PersistSnapshotAsync(
        EmailSendJob job, TemplateContent content, IReadOnlyDictionary<string, string?> jobVars,
        SendAssets? assets, CancellationToken ct)
    {
        if (job.RenderedSnapshotKey is not null) return;
        if (!job.Recipients.Any(r => r.Status == RecipientStatus.Sent)) return;

        var rendered = renderer.Render(new RenderRequest(content, jobVars, Strict: false,
            assets?.Urls ?? new Dictionary<Guid, string>()));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            subject = job.SubjectSnapshot,
            html = rendered.Html,
            text = rendered.Text,
        });

        var key = $"snapshots/{job.UserId}/{job.Id}.json";
        await storage.PutAsync(storageOptions.Value.SnapshotsBucket, key, payload, "application/json", ct);
        job.RenderedSnapshotKey = key;
    }

    private async Task<byte[]> DownloadAsync(string bucket, string key, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(bucket, key, ct);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }

    private void RecordEvent(EmailSendJob job, string type, string? errorCode, int? httpStatus) =>
        db.EmailProviderEvents.Add(new EmailProviderEvent
        {
            ConnectedEmailAccountId = job.ConnectedEmailAccountId,
            SendJobId = job.Id,
            Provider = job.Account!.Provider,
            EventType = type,
            ProviderErrorCode = errorCode,
            HttpStatus = httpStatus,
            CreatedAt = clock.UtcNow,
        });

    private bool IsDue(EmailSendRecipient recipient, DateTimeOffset now) =>
        recipient.Status == RecipientStatus.Pending
        && (recipient.NextAttemptAt is null || recipient.NextAttemptAt <= now);

    private static TimeSpan Backoff(int attemptCount)
    {
        var seconds = 30 * Math.Pow(2, Math.Max(0, attemptCount));
        var jitter = Random.Shared.Next(0, 10);
        return TimeSpan.FromSeconds(Math.Min(seconds, 480) + jitter); // cap at 8 min
    }

    private static IReadOnlyDictionary<string, string?> Deserialize(JsonDocument document)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return result;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();
        }
        return result;
    }

    private sealed record SendAssets(
        IReadOnlyDictionary<Guid, string> Urls,
        List<CidAttachment> Inline,
        List<FileAttachment> Attachments) : IDisposable
    {
        public void Dispose() { }
    }
}
