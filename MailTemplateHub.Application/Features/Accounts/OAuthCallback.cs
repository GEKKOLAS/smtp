using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Accounts;

/// <summary>Callback failed before an account could be established; carries a safe redirect code.</summary>
public sealed class OAuthCallbackException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed record OAuthCallbackResult(string ReturnTo, EmailProvider Provider, bool ScopeMissing);

/// <summary>
/// Completes an OAuth connect (spec 04-security.md §2): validates the single-use,
/// session-bound state, exchanges the code with the PKCE verifier, fetches the
/// provider profile, upserts the account, and stores encrypted tokens.
/// </summary>
public sealed class OAuthCallbackHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IOAuthProviderResolver providers,
    ITokenCipher tokenCipher,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<OAuthCallbackResult> HandleAsync(
        EmailProvider provider, string? code, string? stateId, string redirectUri, CancellationToken ct)
    {
        var state = await ValidateStateAsync(provider, stateId, ct);

        // State is single-use: consume it regardless of what happens next.
        db.OAuthStates.Remove(state);

        if (string.IsNullOrEmpty(code))
        {
            await db.SaveChangesAsync(ct);
            throw new OAuthCallbackException("oauth.exchange_failed");
        }

        var service = providers.For(provider);

        OAuthTokenResponse tokens;
        ProviderProfile profile;
        try
        {
            tokens = await service.ExchangeCodeAsync(code, state.PkceVerifier, redirectUri, ct);
            profile = await service.GetProfileAsync(tokens.AccessToken, tokens.IdToken, ct);
        }
        catch (Exception ex) when (ex is OAuthExchangeException or OAuthTransientException)
        {
            await db.SaveChangesAsync(ct);
            throw new OAuthCallbackException("oauth.exchange_failed");
        }

        var scopeMissing = !HasRequiredScopes(service, tokens.GrantedScopes);
        var account = await UpsertAccountAsync(provider, profile, tokens, scopeMissing, ct);
        StoreTokens(account, tokens);

        audit.Add(AuditActions.AccountConnected, currentUser.UserId, "connected_email_account", account.Id,
            new { provider = provider.ToString().ToLowerInvariant(), scopeMissing });
        await db.SaveChangesAsync(ct);

        return new OAuthCallbackResult(state.ReturnTo, provider, scopeMissing);
    }

    private async Task<OAuthState> ValidateStateAsync(EmailProvider provider, string? stateId, CancellationToken ct)
    {
        if (!Guid.TryParse(stateId, out var id))
        {
            await RejectStateAsync(ct);
            throw new OAuthCallbackException("oauth.state_invalid");
        }

        var state = await db.OAuthStates.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (state is null
            || state.Provider != provider
            || state.UserId != currentUser.UserId
            || !state.IsUsable(clock.UtcNow))
        {
            await RejectStateAsync(ct);
            throw new OAuthCallbackException("oauth.state_invalid");
        }

        return state;
    }

    private async Task RejectStateAsync(CancellationToken ct)
    {
        audit.Add(AuditActions.OAuthStateRejected, currentUser.UserId);
        await db.SaveChangesAsync(ct);
    }

    private static bool HasRequiredScopes(IOAuthProviderService service, IReadOnlyList<string> granted)
    {
        var grantedSet = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
        return service.RequiredScopes.All(grantedSet.Contains);
    }

    private async Task<ConnectedEmailAccount> UpsertAccountAsync(
        EmailProvider provider, ProviderProfile profile, OAuthTokenResponse tokens,
        bool scopeMissing, CancellationToken ct)
    {
        // Reconnect revives a prior (possibly disconnected) row so its id stays stable.
        var account = await db.ConnectedEmailAccounts
            .IgnoreQueryFilters()
            .Include(a => a.Token)
            .FirstOrDefaultAsync(
                a => a.UserId == currentUser.UserId
                     && a.Provider == provider
                     && a.ProviderAccountId == profile.ProviderAccountId,
                ct);

        var now = clock.UtcNow;

        if (account is null)
        {
            account = new ConnectedEmailAccount
            {
                UserId = currentUser.UserId,
                Provider = provider,
                ProviderAccountId = profile.ProviderAccountId,
                EmailAddress = profile.Email,
            };
            db.ConnectedEmailAccounts.Add(account);
        }

        account.EmailAddress = profile.Email;
        account.DisplayName = profile.DisplayName;
        account.TenantId = profile.TenantId;
        account.GrantedScopes = [.. tokens.GrantedScopes];
        account.DeletedAt = null;
        account.ConnectedAt = now;
        account.State = scopeMissing ? AccountState.NeedsReconnect : AccountState.Active;
        account.StateReason = scopeMissing ? AccountStateReasons.InsufficientScope : null;

        return account;
    }

    private void StoreTokens(ConnectedEmailAccount account, OAuthTokenResponse tokens)
    {
        if (account.Token is null)
        {
            var (wrappedDek, kekVersion) = tokenCipher.CreateDataKey();
            var access = tokenCipher.Encrypt(wrappedDek, kekVersion, tokens.AccessToken, account.Id);
            var refresh = tokens.RefreshToken is null
                ? null
                : tokenCipher.Encrypt(wrappedDek, kekVersion, tokens.RefreshToken, account.Id);

            account.Token = new OAuthToken
            {
                ConnectedEmailAccountId = account.Id,
                AccessTokenCiphertext = access.Ciphertext,
                AccessTokenNonce = access.Nonce,
                RefreshTokenCiphertext = refresh?.Ciphertext,
                RefreshTokenNonce = refresh?.Nonce,
                WrappedDek = wrappedDek,
                KekVersion = kekVersion,
                AccessTokenExpiresAt = tokens.AccessTokenExpiresAt,
            };
            db.OAuthTokens.Add(account.Token);
        }
        else
        {
            // Reuse the account's existing DEK so a retained refresh token (when the
            // provider omits a new one on re-consent) still decrypts.
            var token = account.Token;
            var access = tokenCipher.Encrypt(token.WrappedDek, token.KekVersion, tokens.AccessToken, account.Id);
            token.AccessTokenCiphertext = access.Ciphertext;
            token.AccessTokenNonce = access.Nonce;
            if (tokens.RefreshToken is not null)
            {
                var refresh = tokenCipher.Encrypt(token.WrappedDek, token.KekVersion, tokens.RefreshToken, account.Id);
                token.RefreshTokenCiphertext = refresh.Ciphertext;
                token.RefreshTokenNonce = refresh.Nonce;
            }
            token.AccessTokenExpiresAt = tokens.AccessTokenExpiresAt;
            token.RefreshFailureCount = 0;
        }
    }
}
