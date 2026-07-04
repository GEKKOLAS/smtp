using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Providers.Microsoft;

internal sealed class MicrosoftOAuthService(HttpClient httpClient, IOptions<MicrosoftOAuthOptions> options)
    : OAuthProviderServiceBase(httpClient, options.Value)
{
    public override EmailProvider Provider => EmailProvider.Outlook;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraAuthorizationParameters() =>
    [
        new("response_mode", "query"),
    ];

    // Microsoft requires the scopes on refresh too.
    protected override IEnumerable<KeyValuePair<string, string>> ExtraRefreshParameters() =>
    [
        new("scope", string.Join(' ', Options.Scopes)),
    ];

    public override async Task<ProviderProfile> GetProfileAsync(
        string accessToken, string? idToken, CancellationToken ct)
    {
        // Graph /me: id + mailbox address; tenant/object id come from the id_token.
        var me = await GetJsonAsync(Options.UserInfoEndpoint, accessToken, ct);
        var email = StringOrNull(me, "mail")
            ?? StringOrNull(me, "userPrincipalName")
            ?? throw new OAuthExchangeException("Graph /me is missing a mailbox address.");

        // Prefer the stable object id (oid) from the id_token; fall back to Graph id.
        var providerAccountId = JwtPayloadReader.GetClaim(idToken, "oid")
            ?? StringOrNull(me, "id")
            ?? throw new OAuthExchangeException("Graph /me is missing id.");
        var tenantId = JwtPayloadReader.GetClaim(idToken, "tid");

        return new ProviderProfile(providerAccountId, email, StringOrNull(me, "displayName"), tenantId);
    }
}
