using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Providers.Google;

internal sealed class GoogleOAuthService(HttpClient httpClient, IOptions<GoogleOAuthOptions> options)
    : OAuthProviderServiceBase(httpClient, options.Value)
{
    public override EmailProvider Provider => EmailProvider.Gmail;

    // access_type=offline + prompt=consent guarantee a refresh token (spec 07 §1).
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraAuthorizationParameters() =>
    [
        new("access_type", "offline"),
        new("prompt", "consent"),
        new("include_granted_scopes", "false"),
    ];

    public override async Task<ProviderProfile> GetProfileAsync(
        string accessToken, string? idToken, CancellationToken ct)
    {
        // Google userinfo: stable subject id + verified email.
        var profile = await GetJsonAsync(Options.UserInfoEndpoint, accessToken, ct);
        var sub = StringOrNull(profile, "sub")
            ?? throw new OAuthExchangeException("Google userinfo is missing sub.");
        var email = StringOrNull(profile, "email")
            ?? throw new OAuthExchangeException("Google userinfo is missing email.");
        return new ProviderProfile(sub, email, StringOrNull(profile, "name"), TenantId: null);
    }
}
