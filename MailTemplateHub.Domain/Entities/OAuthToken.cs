namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// 1:1 with <see cref="ConnectedEmailAccount"/>. Stores ciphertext only:
/// AES-256-GCM envelope encryption with a per-row DEK wrapped by a versioned
/// KEK (spec 04-security.md §2). No plaintext token ever reaches the database.
/// </summary>
public sealed class OAuthToken
{
    /// <summary>PK == FK to the account (1:1).</summary>
    public required Guid ConnectedEmailAccountId { get; init; }

    public required byte[] AccessTokenCiphertext { get; set; }
    public required byte[] AccessTokenNonce { get; set; }
    public byte[]? RefreshTokenCiphertext { get; set; }
    public byte[]? RefreshTokenNonce { get; set; }

    public required byte[] WrappedDek { get; set; }
    public required int KekVersion { get; set; }

    public required DateTimeOffset AccessTokenExpiresAt { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
    public DateTimeOffset? LastRefreshedAt { get; set; }
    public int RefreshFailureCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ConnectedEmailAccount? Account { get; init; }
}
