using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MailTemplateHub.Infrastructure.Jobs;

/// <summary>
/// Hourly proactive token refresh sweep (spec 10-jobs.md J4). Refreshes active
/// accounts whose access token expires soon so scheduled sends don't pay the
/// refresh latency; invalid_grant handling is inside the refresh service.
/// </summary>
public sealed class RefreshTokensJob(
    IAppDbContext db, ITokenRefreshService tokenRefresh, IClock clock, ILogger<RefreshTokensJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var threshold = clock.UtcNow.AddMinutes(30);
        var accountIds = await db.OAuthTokens
            .Where(t => t.AccessTokenExpiresAt < threshold && t.Account!.State == AccountState.Active)
            .Select(t => t.ConnectedEmailAccountId)
            .ToListAsync(ct);

        int refreshed = 0, failed = 0;
        foreach (var accountId in accountIds)
        {
            try
            {
                await tokenRefresh.GetValidContextAsync(accountId, ct);
                refreshed++;
            }
            catch (RefreshTokenRevokedException)
            {
                failed++; // account already flagged NeedsReconnect by the service
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Token refresh sweep failed for account {AccountId}", accountId);
            }
        }

        if (accountIds.Count > 0)
        {
            logger.LogInformation("Token refresh sweep: {Refreshed} refreshed, {Failed} failed", refreshed, failed);
        }
    }
}
