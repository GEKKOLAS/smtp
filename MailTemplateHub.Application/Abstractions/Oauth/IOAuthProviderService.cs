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

    /// <summary>Scopes we request; used to detect insufficient-scope grants on callback.</summary>
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
