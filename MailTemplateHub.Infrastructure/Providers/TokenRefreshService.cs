using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Infrastructure.Providers;

/// <summary>
/// Returns a valid access token, refreshing under a Postgres advisory lock when
/// close to expiry (spec 04-security.md §2, 07 §2). On invalid_grant it flips the
/// account to NeedsReconnect, wipes tokens, and throws.
/// </summary>
internal sealed class TokenRefreshService(
    IAppDbContext db,
    IOAuthProviderResolver providers,
    ITokenCipher tokenCipher,
    IAuditWriter audit,
    IClock clock)
    : ITokenRefreshService
{
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromMinutes(5);

    public async Task<ConnectedAccountContext> GetValidContextAsync(Guid accountId, CancellationToken ct)
    {
        var account = await LoadAsync(accountId, ct);
        var now = clock.UtcNow;

        if (account.Token!.AccessTokenExpiresAt - now > ExpirySkew)
        {
            return BuildContext(account, DecryptAccess(account));
        }

        // Serialize concurrent refreshes for this account across instances,
        // then re-read since another worker may have refreshed already.
        await using var _ = await db.AcquireAdvisoryLockAsync(AdvisoryKey(account.Id), ct);
        account = await LoadAsync(accountId, ct);
        if (account.Token!.AccessTokenExpiresAt - clock.UtcNow > ExpirySkew)
        {
            return BuildContext(account, DecryptAccess(account));
        }

        return await RefreshAsync(account, ct);
    }

    private async Task<ConnectedAccountContext> RefreshAsync(ConnectedEmailAccount account, CancellationToken ct)
    {
        var token = account.Token!;
        if (token.RefreshTokenCiphertext is null || token.RefreshTokenNonce is null)
        {
            await FlagNeedsReconnectAsync(account, AccountStateReasons.InvalidGrant, ct);
            throw new RefreshTokenRevokedException();
        }

        var refreshToken = tokenCipher.Decrypt(
            token.WrappedDek, token.KekVersion,
            new EncryptedSecret(token.RefreshTokenCiphertext, token.RefreshTokenNonce), account.Id);

        OAuthTokenResponse refreshed;
        try
        {
            refreshed = await providers.For(account.Provider).RefreshAsync(refreshToken, ct);
        }
        catch (RefreshTokenRevokedException)
        {
            await FlagNeedsReconnectAsync(account, AccountStateReasons.InvalidGrant, ct);
            throw;
        }

        var access = tokenCipher.Encrypt(token.WrappedDek, token.KekVersion, refreshed.AccessToken, account.Id);
        token.AccessTokenCiphertext = access.Ciphertext;
        token.AccessTokenNonce = access.Nonce;
        token.AccessTokenExpiresAt = refreshed.AccessTokenExpiresAt;
        token.LastRefreshedAt = clock.UtcNow;
        token.RefreshFailureCount = 0;

        // Microsoft rotates the refresh token on every refresh; persist the new one.
        if (refreshed.RefreshToken is not null)
        {
            var newRefresh = tokenCipher.Encrypt(token.WrappedDek, token.KekVersion, refreshed.RefreshToken, account.Id);
            token.RefreshTokenCiphertext = newRefresh.Ciphertext;
            token.RefreshTokenNonce = newRefresh.Nonce;
        }

        db.EmailProviderEvents.Add(new EmailProviderEvent
        {
            ConnectedEmailAccountId = account.Id,
            Provider = account.Provider,
            EventType = ProviderEventTypes.TokenRefresh,
            CreatedAt = clock.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return BuildContext(account, refreshed.AccessToken);
    }

    private async Task FlagNeedsReconnectAsync(ConnectedEmailAccount account, string reason, CancellationToken ct)
    {
        account.MarkNeedsReconnect(reason);
        if (account.Token is not null)
        {
            account.Token.RefreshFailureCount++;
        }
        db.EmailProviderEvents.Add(new EmailProviderEvent
        {
            ConnectedEmailAccountId = account.Id,
            Provider = account.Provider,
            EventType = ProviderEventTypes.TokenRefreshFailed,
            ProviderErrorCode = reason,
            CreatedAt = clock.UtcNow,
        });
        audit.Add(AuditActions.TokenRefreshFailed, account.UserId, "connected_email_account", account.Id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ConnectedEmailAccount> LoadAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.ConnectedEmailAccounts
            .IgnoreQueryFilters()
            .Include(a => a.Token)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new InvalidOperationException($"Connected account {accountId} not found.");

        if (account.State != AccountState.Active || account.Token is null)
        {
            throw new RefreshTokenRevokedException();
        }
        return account;
    }

    private string DecryptAccess(ConnectedEmailAccount account)
    {
        var token = account.Token!;
        return tokenCipher.Decrypt(
            token.WrappedDek, token.KekVersion,
            new EncryptedSecret(token.AccessTokenCiphertext, token.AccessTokenNonce), account.Id);
    }

    private static ConnectedAccountContext BuildContext(ConnectedEmailAccount account, string accessToken) =>
        new(account.Id, account.Provider, account.EmailAddress, accessToken);

    /// <summary>Stable 64-bit advisory-lock key from the account id (first 8 bytes).</summary>
    private static long AdvisoryKey(Guid accountId) =>
        BitConverter.ToInt64(accountId.ToByteArray(), 0);
}
