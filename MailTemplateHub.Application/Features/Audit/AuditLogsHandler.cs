using System.Text.Json;
using MailTemplateHub.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Audit;

public sealed record AuditLogQuery(string? Action, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);

public sealed record AuditLogDto(
    Guid Id, string Action, string? EntityType, Guid? EntityId, string? Ip,
    JsonElement? Metadata, DateTimeOffset CreatedAt);

public sealed record PagedAuditLogs(IReadOnlyList<AuditLogDto> Items, int Page, int PageSize, int TotalCount);

/// <summary>Read-only view of the caller's own security events (spec 06-api.md §12).</summary>
public sealed class AuditLogsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedAuditLogs> ListAsync(AuditLogQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.AuditLogs.Where(a => a.UserId == currentUser.UserId);
        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(a => a.Action == query.Action);
        if (query.From is { } from) q = q.Where(a => a.CreatedAt >= from);
        if (query.To is { } to) q = q.Where(a => a.CreatedAt <= to);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(a => new AuditLogDto(
            a.Id, a.Action, a.EntityType, a.EntityId, a.Ip,
            a.Metadata is null ? null : JsonDocument.Parse(a.Metadata.RootElement.GetRawText()).RootElement,
            a.CreatedAt)).ToList();

        return new PagedAuditLogs(items, page, pageSize, total);
    }
}
