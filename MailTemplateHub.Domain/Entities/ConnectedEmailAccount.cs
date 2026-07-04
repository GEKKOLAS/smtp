using MailTemplateHub.Domain.Common;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// A Gmail or Microsoft mailbox linked via OAuth, owned by exactly one user
/// (spec 05-database.md). Tokens live in the 1:1 <see cref="OAuthToken"/> so this
/// hot row never carries ciphertext.
/// </summary>
public sealed class ConnectedEmailAccount : IHasTimestamps, ISoftDeletable
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required EmailProvider Provider { get; init; }

    /// <summary>Provider's stable subject id (Google <c>sub</c> / Microsoft <c>oid</c>).</summary>
    public required string ProviderAccountId { get; init; }

    public required string EmailAddress { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>Microsoft tenant id (<c>tid</c>); null for Gmail/personal MSA.</summary>
    public string? TenantId { get; set; }

    public List<string> GrantedScopes { get; set; } = [];
    public AccountState State { get; set; } = AccountState.Active;
    public string? StateReason { get; set; }
    public bool IsDefault { get; set; }

    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public OAuthToken? Token { get; set; }

    public void MarkNeedsReconnect(string reason)
    {
        State = AccountState.NeedsReconnect;
        StateReason = reason;
    }
}
