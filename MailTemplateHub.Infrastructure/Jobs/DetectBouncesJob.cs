using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using MailTemplateHub.Infrastructure.Sending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MailTemplateHub.Infrastructure.Jobs;

/// <summary>
/// Every 15 minutes (spec 14 F4.3, job J6): for each account that sent mail in
/// the last 72 hours and may read its inbox, reads new delivery reports and
/// marks the matching recipients as bounced. A report only ever touches a
/// recipient of a job sent from the same account.
/// </summary>
public sealed class DetectBouncesJob(
    IAppDbContext db,
    ITokenRefreshService tokenRefresh,
    IInboxProviderClientFactory inboxes,
    IClock clock,
    ILogger<DetectBouncesJob> logger)
{
    private static readonly TimeSpan Lookback = TimeSpan.FromHours(72);
    private const int MaxNoticesPerScan = 25;

    public async Task RunAsync(CancellationToken ct)
    {
        var since = clock.UtcNow - Lookback;
        var accountIds = await db.EmailSendJobs
            .Where(j => !j.IsTest && j.Recipients.Any(r => r.Status == RecipientStatus.Sent && r.LastAttemptAt >= since))
            .Select(j => j.ConnectedEmailAccountId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var accountId in accountIds)
        {
            var account = await db.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.State == AccountState.Active, ct);
            if (account is null
                || !ProviderScopes.HasScope(account.GrantedScopes, ProviderScopes.InboxReadScope(account.Provider)))
            {
                continue;
            }

            try
            {
                await ScanAsync(account, ct);
            }
            catch (Exception ex) when (ex is ProviderSendException or RefreshTokenRevokedException)
            {
                // One mailbox failing must not stop the others; the next run retries.
                logger.LogWarning(ex, "Bounce scan skipped for account {AccountId}", account.Id);
            }
        }
    }

    private async Task ScanAsync(ConnectedEmailAccount account, CancellationToken ct)
    {
        var context = await tokenRefresh.GetValidContextAsync(account.Id, ct);
        var client = inboxes.For(account.Provider);

        var ids = await client.FindDeliveryNotificationsAsync(context, MaxNoticesPerScan, ct);
        if (ids.Count == 0) return;
        var seen = await db.ProcessedInboxNotices
            .Where(n => n.ConnectedEmailAccountId == account.Id && ids.Contains(n.ProviderMessageId))
            .Select(n => n.ProviderMessageId)
            .ToListAsync(ct);

        var bounced = 0;
        foreach (var id in ids.Except(seen))
        {
            var raw = await client.GetRawMessageAsync(context, id, ct);
            foreach (var notice in DeliveryStatusParser.Parse(raw))
            {
                var recipient = await db.EmailSendRecipients.FirstOrDefaultAsync(r =>
                    r.Id == notice.RecipientRef
                    && db.EmailSendJobs.Any(j => j.Id == r.SendJobId && j.ConnectedEmailAccountId == account.Id), ct);
                if (recipient is null || !recipient.MarkBounced(notice.Status, notice.Diagnostic, clock.UtcNow)) continue;

                bounced++;
                db.EmailProviderEvents.Add(new EmailProviderEvent
                {
                    ConnectedEmailAccountId = account.Id,
                    SendJobId = recipient.SendJobId,
                    Provider = account.Provider,
                    EventType = ProviderEventTypes.Bounce,
                    ProviderErrorCode = notice.Status,
                    CreatedAt = clock.UtcNow,
                });
            }

            db.ProcessedInboxNotices.Add(new ProcessedInboxNotice
            {
                ConnectedEmailAccountId = account.Id,
                ProviderMessageId = id,
                ProcessedAt = clock.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        if (bounced > 0) logger.LogInformation("Marked {Count} recipients bounced for account {AccountId}", bounced, account.Id);
    }
}
