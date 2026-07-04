using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// Transient server-side state backing the OAuth <c>state</c> parameter
/// (spec 04-security.md §2). Holds the PKCE verifier, is single-use, short-lived,
/// and bound to the initiating user's session. Deleted on use; swept when expired.
/// </summary>
public sealed class OAuthState
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required EmailProvider Provider { get; init; }
    public required string PkceVerifier { get; init; }
    public required string ReturnTo { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    public bool IsUsable(DateTimeOffset now) => now < ExpiresAt;
}
