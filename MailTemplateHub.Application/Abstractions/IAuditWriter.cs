namespace MailTemplateHub.Application.Abstractions;

/// <summary>
/// Stages an audit entry on the current unit of work; it is persisted by the
/// handler's SaveChangesAsync so action and audit commit atomically.
/// </summary>
public interface IAuditWriter
{
    void Add(string action, Guid? userId, string? entityType = null, Guid? entityId = null, object? metadata = null);
}
