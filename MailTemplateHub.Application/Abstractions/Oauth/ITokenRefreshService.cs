using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Abstractions.Oauth;

/// <summary>Decrypted, valid token context for a connected account.</summary>
public sealed record ConnectedAccountContext(
    Guid AccountId,
    EmailProvider Provider,
    string EmailAddress,
    string AccessToken);

/// <summary>
/// Returns a valid access token for an account, refreshing under an advisory
/// lock when it is close to expiry. On permanent refresh failure it flips the
/// account to NeedsReconnect and throws (spec 04-security.md §2, 07 §2).
/// </summary>
public interface ITokenRefreshService
{
    Task<ConnectedAccountContext> GetValidContextAsync(Guid accountId, CancellationToken ct);
}
