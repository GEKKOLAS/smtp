using System.ComponentModel.DataAnnotations;

namespace MailTemplateHub.Application.Common;

/// <summary>
/// Endpoints are configurable so tests point them at an in-process/WireMock
/// double instead of live Google/Microsoft (spec 11-testing.md).
/// </summary>
public abstract class OAuthProviderOptions
{
    [Required] public string ClientId { get; init; } = string.Empty;
    [Required] public string ClientSecret { get; init; } = string.Empty;
    [Required] public string AuthorizationEndpoint { get; init; } = string.Empty;
    [Required] public string TokenEndpoint { get; init; } = string.Empty;
    [Required] public string UserInfoEndpoint { get; init; } = string.Empty;
    public string? RevokeEndpoint { get; init; }

    public string[] Scopes { get; init; } = [];
}

public sealed class GoogleOAuthOptions : OAuthProviderOptions
{
    public const string SectionName = "OAuth:Google";
}

public sealed class MicrosoftOAuthOptions : OAuthProviderOptions
{
    public const string SectionName = "OAuth:Microsoft";
}

public sealed class OAuthGeneralOptions
{
    public const string SectionName = "OAuth";

    /// <summary>Public base URL of the API, used to build the exact registered redirect URI.</summary>
    public string RedirectBaseUrl { get; init; } = "http://localhost:5001";

    /// <summary>Where the callback 302s the browser afterward (the SPA origin).</summary>
    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";

    /// <summary>Per-user cap on connected accounts (spec 06-api.md §3).</summary>
    public int MaxAccountsPerUser { get; init; } = 5;

    public int StateTtlMinutes { get; init; } = 10;
}
