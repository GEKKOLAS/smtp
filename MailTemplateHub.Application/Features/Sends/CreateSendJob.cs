using System.Text.Json;
using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Templates;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Sends;

/// <summary>
/// Validates and queues a send (spec 06 §9, 07 §4): checks the account, template
/// version, recipients, per-recipient required variables, attachments, and the
/// 25 MB size budget; snapshots the subject; then enqueues the job.
/// </summary>
public sealed partial class CreateSendJobHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITemplateRenderer renderer,
    IBackgroundJobScheduler jobs,
    IAuditWriter audit,
    IOptions<SendLimitsOptions> limitsOptions,
    IClock clock)
{
    public async Task<SendJobDto> HandleAsync(CreateSendCommand command, CancellationToken ct)
    {
        var limits = limitsOptions.Value;

        // Idempotency: replay returns the original job.
        if (!string.IsNullOrEmpty(command.IdempotencyKey))
        {
            var existing = await db.EmailSendJobs
                .Include(j => j.Recipients)
                .FirstOrDefaultAsync(
                    j => j.UserId == currentUser.UserId && j.IdempotencyKey == command.IdempotencyKey, ct);
            if (existing is not null) return SendJobDto.From(existing);
        }

        ValidateRecipients(command.Recipients, limits);
        ValidateSchedule(command.ScheduledAt, limits);

        var account = await db.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a => a.Id == command.ConnectedEmailAccountId && a.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        if (account.State != AccountState.Active)
        {
            throw new ConflictException("send.account_needs_reconnect", "This account needs to be reconnected.");
        }

        var version = await db.EmailTemplateVersions
            .Include(v => v.TemplateAssets).ThenInclude(ta => ta.Asset)
            .Include(v => v.Template!)
            .FirstOrDefaultAsync(v => v.Id == command.TemplateVersionId && v.Template!.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        var content = TemplateContentMapping.ToRenderContent(version);
        ValidateVariablesPerRecipient(content.Variables, command.Variables, command.Recipients);

        var attachments = await BuildAttachmentsAsync(command.Attachments, ct);
        await EnsureWithinBudgetAsync(content, command.Variables, version, attachments, limits, ct);

        var subjectSnapshot = RenderSubject(content, command.Variables);
        var now = clock.UtcNow;
        var scheduled = command.ScheduledAt is not null;

        var job = new EmailSendJob
        {
            UserId = currentUser.UserId,
            ConnectedEmailAccountId = account.Id,
            TemplateVersionId = version.Id,
            Status = scheduled ? SendJobStatus.Scheduled : SendJobStatus.Queued,
            SubjectSnapshot = subjectSnapshot,
            VariableValues = JsonSerializer.SerializeToDocument(command.Variables),
            ScheduledAt = command.ScheduledAt,
            QueuedAt = scheduled ? null : now,
            IdempotencyKey = command.IdempotencyKey,
            Recipients = BuildRecipients(command.Recipients, now),
            Attachments = attachments,
        };
        db.EmailSendJobs.Add(job);

        audit.Add(scheduled ? AuditActions.SendScheduled : AuditActions.SendCreated,
            currentUser.UserId, "email_send_job", job.Id,
            new { recipients = job.Recipients.Count, scheduled });
        await db.SaveChangesAsync(ct);

        if (!scheduled) jobs.EnqueueSend(job.Id);
        return SendJobDto.From(job);
    }

    private void ValidateRecipients(IReadOnlyList<RecipientInput> recipients, SendLimitsOptions limits)
    {
        if (recipients.Count == 0)
        {
            throw new ValidationFailure("recipients", "At least one recipient is required.");
        }
        if (recipients.Count > limits.MaxRecipients)
        {
            throw new ValidationFailure("recipients", $"At most {limits.MaxRecipients} recipients are allowed.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipient in recipients)
        {
            if (!EmailRegex().IsMatch(recipient.Email) || recipient.Email.Any(char.IsControl))
            {
                throw new ValidationFailure("recipients", $"Invalid email address '{recipient.Email}'.");
            }
            if (!seen.Add(recipient.Email))
            {
                throw new ValidationFailure("recipients", $"Duplicate recipient '{recipient.Email}'.");
            }
        }
    }

    private void ValidateSchedule(DateTimeOffset? scheduledAt, SendLimitsOptions limits)
    {
        if (scheduledAt is null) return;
        var now = clock.UtcNow;
        if (scheduledAt < now.AddMinutes(limits.MinScheduleMinutes))
        {
            throw new ValidationFailure("scheduledAt", $"Schedule at least {limits.MinScheduleMinutes} minutes ahead.");
        }
        if (scheduledAt > now.AddDays(limits.MaxScheduleDays))
        {
            throw new ValidationFailure("scheduledAt", "The scheduled time is too far in the future.");
        }
    }

    private static void ValidateVariablesPerRecipient(
        IReadOnlyList<TemplateVariable> schema,
        IReadOnlyDictionary<string, string?> jobVars,
        IReadOnlyList<RecipientInput> recipients)
    {
        var required = schema.Where(v => v.Required).ToList();
        if (required.Count == 0) return;

        var perRecipientMissing = new List<object>();
        foreach (var recipient in recipients)
        {
            var missing = required
                .Where(v => !HasValue(v, jobVars, recipient.VariableOverrides))
                .Select(v => v.Name)
                .ToList();
            if (missing.Count > 0)
            {
                perRecipientMissing.Add(new { recipientEmail = recipient.Email, missing });
            }
        }

        if (perRecipientMissing.Count > 0)
        {
            throw new MissingRecipientVariablesException(perRecipientMissing);
        }

        static bool HasValue(TemplateVariable v, IReadOnlyDictionary<string, string?> job,
            IReadOnlyDictionary<string, string?>? overrides)
        {
            if (overrides is not null && overrides.TryGetValue(v.Name, out var o) && !string.IsNullOrEmpty(o)) return true;
            if (job.TryGetValue(v.Name, out var j) && !string.IsNullOrEmpty(j)) return true;
            return !string.IsNullOrEmpty(v.Default);
        }
    }

    private async Task<List<EmailSendAttachment>> BuildAttachmentsAsync(
        IReadOnlyList<SendAttachmentInput> inputs, CancellationToken ct)
    {
        if (inputs.Count == 0) return [];
        var ids = inputs.Select(a => a.AssetId).Distinct().ToList();
        var owned = await db.Assets
            .Where(a => ids.Contains(a.Id) && a.UserId == currentUser.UserId && a.UploadState == AssetUploadState.Ready)
            .Select(a => a.Id)
            .ToListAsync(ct);
        var ownedSet = owned.ToHashSet();

        var result = new List<EmailSendAttachment>();
        foreach (var input in inputs)
        {
            if (!ownedSet.Contains(input.AssetId))
            {
                throw new ValidationFailure("attachments", "An attachment was not found.");
            }
            var disposition = string.Equals(input.Disposition, "inline", StringComparison.OrdinalIgnoreCase)
                ? SendAttachmentDisposition.Inline
                : SendAttachmentDisposition.Attachment;
            result.Add(new EmailSendAttachment
            {
                SendJobId = default,
                AssetId = input.AssetId,
                Disposition = disposition,
                FilenameOverride = input.FilenameOverride,
                CreatedAt = clock.UtcNow,
            });
        }
        return result;
    }

    private async Task EnsureWithinBudgetAsync(
        Abstractions.Rendering.TemplateContent content,
        IReadOnlyDictionary<string, string?> jobVars,
        EmailTemplateVersion version,
        List<EmailSendAttachment> attachments,
        SendLimitsOptions limits,
        CancellationToken ct)
    {
        // Approximate: rendered body bytes + inline/attachment asset bytes (base64 inflated).
        var rendered = renderer.Render(new RenderRequest(content, jobVars, Strict: false, EmptyAssetUrls));
        long total = System.Text.Encoding.UTF8.GetByteCount(rendered.Html)
                     + System.Text.Encoding.UTF8.GetByteCount(rendered.Text);

        var inlineAssetIds = version.TemplateAssets
            .Where(ta => ta.Usage == TemplateAssetUsage.InlineCid)
            .Select(ta => ta.AssetId);
        var attachmentAssetIds = attachments.Select(a => a.AssetId);
        var assetIds = inlineAssetIds.Concat(attachmentAssetIds).Distinct().ToList();

        if (assetIds.Count > 0)
        {
            var sizes = await db.Assets.Where(a => assetIds.Contains(a.Id)).SumAsync(a => a.SizeBytes, ct);
            total += (long)(sizes * 1.37); // base64 inflation
        }

        if (total > limits.MaxMessageBytes)
        {
            throw new SendTooLargeException(total, limits.MaxMessageBytes);
        }
    }

    private string RenderSubject(Abstractions.Rendering.TemplateContent content, IReadOnlyDictionary<string, string?> jobVars)
    {
        var rendered = renderer.Render(new RenderRequest(content, jobVars, Strict: false, EmptyAssetUrls));
        return rendered.Subject;
    }

    private List<EmailSendRecipient> BuildRecipients(IReadOnlyList<RecipientInput> inputs, DateTimeOffset now) =>
        inputs.Select(r => new EmailSendRecipient
        {
            SendJobId = default,
            ContactId = r.ContactId,
            EmailAddress = r.Email,
            DisplayName = r.Name,
            VariableOverrides = r.VariableOverrides is { Count: > 0 }
                ? JsonSerializer.SerializeToDocument(r.VariableOverrides)
                : JsonDocument.Parse("{}"),
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

    private static readonly IReadOnlyDictionary<Guid, string> EmptyAssetUrls = new Dictionary<Guid, string>();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}

public sealed class MissingRecipientVariablesException(IReadOnlyList<object> perRecipient)
    : AppException("send.variables_missing", "Some recipients are missing required variables.")
{
    public IReadOnlyList<object> PerRecipient { get; } = perRecipient;
}

public sealed class SendTooLargeException(long actualBytes, long maxBytes)
    : AppException("send.too_large", $"The message is {actualBytes / (1024 * 1024)} MB; the limit is {maxBytes / (1024 * 1024)} MB.")
{
    public long ActualBytes { get; } = actualBytes;
    public long MaxBytes { get; } = maxBytes;
}
