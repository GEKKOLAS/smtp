namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// Server-side session. The cookie carries a random token; only its SHA-256
/// hash is stored here (spec 04-security.md §1).
/// </summary>
public sealed class UserSession
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required byte[] TokenHash { get; init; }
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; init; }

    public User? User { get; init; }

    public bool IsActive(DateTimeOffset now, TimeSpan idleTimeout) =>
        now < ExpiresAt && now < LastSeenAt + idleTimeout;
}
