using System.Text.Json;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Domain.Entities;

namespace MailTemplateHub.Infrastructure.Audit;

public sealed class AuditWriter(IAppDbContext db, IRequestContext requestContext, IClock clock) : IAuditWriter
{
    public void Add(string action, Guid? userId, string? entityType = null, Guid? entityId = null, object? metadata = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Ip = requestContext.Ip,
            UserAgent = requestContext.UserAgent,
            Metadata = metadata is null ? null : JsonSerializer.SerializeToDocument(metadata),
            CreatedAt = clock.UtcNow,
        });
    }
}
