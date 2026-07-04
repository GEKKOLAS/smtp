using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Accounts;

public sealed record AccountTestResult(bool Ok, string? Email, string? DisplayName, string? ErrorCode);

public sealed class AccountsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IOAuthProviderResolver providers,
    ITokenRefreshService tokenRefresh,
    ITokenCipher tokenCipher,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken ct)
    {
        var accounts = await db.ConnectedEmailAccounts
            .Where(a => a.UserId == currentUser.UserId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.ConnectedAt)
            .ToListAsync(ct);
        return accounts.Select(AccountDto.From).ToList();
    }

    public async Task<AccountDto> GetAsync(Guid id, CancellationToken ct)
    {
        var account = await FindOwnedAsync(id, ct);
        return AccountDto.From(account);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken ct)
    {
        var account = await FindOwnedAsync(id, ct);
        if (account.State == AccountState.Revoked) throw new NotFoundException();

        // At most one default per user; clear the others in the same unit of work.
        await db.ConnectedEmailAccounts
            .Where(a => a.UserId == currentUser.UserId && a.IsDefault && a.Id != id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

        account.IsDefault = true;
        audit.Add(AuditActions.AccountDefaultChanged, currentUser.UserId, "connected_email_account", id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Live "who am I" call to confirm the connection still works.</summary>
    public async Task<AccountTestResult> TestAsync(Guid id, CancellationToken ct)
    {
        var account = await FindOwnedAsync(id, ct);
        if (account.State != AccountState.Active)
        {
            return new AccountTestResult(false, null, null, "account.needs_reconnect");
        }

        try
        {
            var context = await tokenRefresh.GetValidContextAsync(id, ct);
            var profile = await providers.For(account.Provider)
                .GetProfileAsync(context.AccessToken, idToken: null, ct);

            account.LastUsedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return new AccountTestResult(true, profile.Email, profile.DisplayName, null);
        }
        catch (RefreshTokenRevokedException)
        {
            return new AccountTestResult(false, null, null, "account.needs_reconnect");
        }
        catch (Exception ex) when (ex is OAuthTransientException or OAuthExchangeException)
        {
            return new AccountTestResult(false, null, null, "provider.unavailable");
        }
    }

    public async Task DisconnectAsync(Guid id, CancellationToken ct)
    {
        var account = await db.ConnectedEmailAccounts
            .Include(a => a.Token)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        // Best-effort provider revocation before wiping local tokens.
        if (account.Token?.RefreshTokenCiphertext is { } cipher && account.Token.RefreshTokenNonce is { } nonce)
        {
            try
            {
                var refreshToken = tokenCipher.Decrypt(
                    account.Token.WrappedDek, account.Token.KekVersion,
                    new EncryptedSecret(cipher, nonce), account.Id);
                await providers.For(account.Provider).RevokeAsync(refreshToken, ct);
            }
            catch
            {
                // Revocation is best-effort; local wipe proceeds regardless.
            }
        }

        if (account.Token is not null) db.OAuthTokens.Remove(account.Token);
        account.State = AccountState.Revoked;
        account.StateReason = AccountStateReasons.UserDisconnect;
        account.IsDefault = false;
        account.DeletedAt = clock.UtcNow;

        audit.Add(AuditActions.AccountDisconnected, currentUser.UserId, "connected_email_account", id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ConnectedEmailAccount> FindOwnedAsync(Guid id, CancellationToken ct) =>
        await db.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();
}
