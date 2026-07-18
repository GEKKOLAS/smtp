using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Abstractions.Oauth;

public sealed record OAuthTokenResponse(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    IReadOnlyList<string> GrantedScopes,
    string? IdToken);

public sealed record ProviderProfile(
    string ProviderAccountId,
    string Email,
    string? DisplayName,
    string? TenantId);

/// <summary>
/// Provider-specific OAuth operations, kept behind this port so controllers and
/// handlers never touch a Google/Microsoft SDK type (spec 03-architecture.md §3,
/// 07-providers.md). One implementation per provider, resolved by
/// <see cref="IOAuthProviderResolver"/>.
/// </summary>
public interface IOAuthProviderService
{
    EmailProvider Provider { get; }

    /// <summary>
    /// The scope(s) that must actually be granted for the account to be usable for
    /// sending — deliberately narrower than the full list requested. Providers
    /// (Microsoft in particular) don't reliably echo pure-identity scopes
    /// (openid/profile/email/offline_access) back in the token response's "scope"
    /// field even when granted, so gating on those produces false "insufficient
    /// scope" failures for every real connection.
    /// </summary>
    IReadOnlyList<string> RequiredScopes { get; }

    string BuildAuthorizationUrl(string state, string codeChallenge, string redirectUri);

    Task<OAuthTokenResponse> ExchangeCodeAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct);

    Task<OAuthTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);

    Task<ProviderProfile> GetProfileAsync(
        string accessToken, string? idToken, CancellationToken ct);

    /// <summary>Best-effort revocation; failures are swallowed by the caller.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}

public interface IOAuthProviderResolver
{
    IOAuthProviderService For(EmailProvider provider);
}
