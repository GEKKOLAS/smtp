namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// A personal access token for programmatic API use (automation/n8n). The full
/// secret is shown once at creation; only its SHA-256 hash is stored.
/// </summary>
public sealed class ApiKey
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Name { get; set; }

    /// <summary>Display-only leading segment, e.g. "mth_a1b2c3".</summary>
    public required string Prefix { get; init; }

    public required byte[] KeyHash { get; init; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }

    public User? User { get; init; }

    public bool IsUsable(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || now < ExpiresAt);
}
