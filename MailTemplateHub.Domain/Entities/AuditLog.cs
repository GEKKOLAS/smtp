using System.Text.Json;

namespace MailTemplateHub.Domain.Entities;

/// <summary>Append-only security event trail (spec 04-security.md §7).</summary>
public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid? UserId { get; init; }
    public required string Action { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public JsonDocument? Metadata { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
