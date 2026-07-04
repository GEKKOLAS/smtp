using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Accounts;

public sealed record ConnectStartResult(string AuthorizationUrl);

/// <summary>
/// Begins an OAuth connect: creates a single-use, session-bound state row holding
/// the PKCE verifier and returns the provider consent URL (spec 04-security.md §2).
/// </summary>
public sealed class ConnectStartHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IOAuthProviderResolver providers,
    IOptions<OAuthGeneralOptions> generalOptions,
    IClock clock)
{
    private static readonly HashSet<string> AllowedReturnPaths =
        new(StringComparer.Ordinal) { "/accounts", "/dashboard", "/settings" };

    public async Task<ConnectStartResult> HandleAsync(
        EmailProvider provider, string? returnTo, CancellationToken ct)
    {
        var options = generalOptions.Value;

        var activeCount = await db.ConnectedEmailAccounts
            .CountAsync(a => a.UserId == currentUser.UserId && a.State != AccountState.Revoked, ct);
        if (activeCount >= options.MaxAccountsPerUser)
        {
            throw new ConflictException("oauth.account_limit",
                "You have reached the maximum number of connected accounts.");
        }

        var safeReturnTo = returnTo is not null && AllowedReturnPaths.Contains(returnTo)
            ? returnTo
            : "/accounts";

        var service = providers.For(provider);
        var verifier = Pkce.CreateVerifier();
        var now = clock.UtcNow;

        var state = new OAuthState
        {
            UserId = currentUser.UserId,
            Provider = provider,
            PkceVerifier = verifier,
            ReturnTo = safeReturnTo,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(options.StateTtlMinutes),
        };
        db.OAuthStates.Add(state);
        await db.SaveChangesAsync(ct);

        var redirectUri = BuildRedirectUri(options.RedirectBaseUrl, provider);
        var url = service.BuildAuthorizationUrl(state.Id.ToString(), Pkce.Challenge(verifier), redirectUri);
        return new ConnectStartResult(url);
    }

    public static string BuildRedirectUri(string baseUrl, EmailProvider provider) =>
        $"{baseUrl.TrimEnd('/')}/api/v1/oauth/{provider.ToString().ToLowerInvariant()}/callback";
}
