using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Templates;

public sealed record PagedVersions(IReadOnlyList<TemplateVersionSummaryDto> Items, int Page, int PageSize, int TotalCount);

public sealed class TemplateVersionsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    TemplateVersionFactory versionFactory,
    IAuditWriter audit)
{
    public async Task<PagedVersions> ListAsync(Guid templateId, int page, int pageSize, CancellationToken ct)
    {
        await EnsureTemplateOwnedAsync(templateId, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.EmailTemplateVersions.Where(v => v.TemplateId == templateId);
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(v => v.VersionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new TemplateVersionSummaryDto(
                v.Id, v.VersionNumber, v.EditorKind.ToString().ToLower(), v.CreatedAt))
            .ToListAsync(ct);

        return new PagedVersions(items, page, pageSize, total);
    }

    public async Task<TemplateVersionDto> GetAsync(Guid templateId, Guid versionId, CancellationToken ct)
    {
        await EnsureTemplateOwnedAsync(templateId, ct);
        var version = await db.EmailTemplateVersions
            .Include(v => v.TemplateAssets)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, ct)
            ?? throw new NotFoundException();
        return TemplateVersionDto.From(version);
    }

    /// <summary>Saves editor content as a new immutable version, made current.</summary>
    public async Task<TemplateVersionDto> SaveAsync(Guid templateId, TemplateContentInput content, CancellationToken ct)
    {
        var template = await FindOwnedAsync(templateId, ct);
        var next = await NextVersionNumberAsync(templateId, ct);

        var version = await versionFactory.BuildAsync(templateId, next, content, ct);
        db.EmailTemplateVersions.Add(version);
        template.CurrentVersionId = version.Id;

        audit.Add(AuditActions.TemplateVersionSaved, currentUser.UserId, "email_template", templateId,
            new { versionNumber = next });
        await db.SaveChangesAsync(ct);

        return await GetAsync(templateId, version.Id, ct);
    }

    /// <summary>Restore = copy an old version as a new current version (immutability preserved).</summary>
    public async Task<TemplateVersionDto> RestoreAsync(Guid templateId, Guid versionId, CancellationToken ct)
    {
        var template = await FindOwnedAsync(templateId, ct);
        var source = await db.EmailTemplateVersions
            .Include(v => v.TemplateAssets)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, ct)
            ?? throw new NotFoundException();

        var next = await NextVersionNumberAsync(templateId, ct);
        var restored = TemplatesHandler.CloneVersion(source, templateId, next);
        db.EmailTemplateVersions.Add(restored);
        template.CurrentVersionId = restored.Id;

        audit.Add(AuditActions.TemplateRestored, currentUser.UserId, "email_template", templateId,
            new { restoredFrom = source.VersionNumber, newVersion = next });
        await db.SaveChangesAsync(ct);

        return await GetAsync(templateId, restored.Id, ct);
    }

    private async Task<int> NextVersionNumberAsync(Guid templateId, CancellationToken ct)
    {
        var max = await db.EmailTemplateVersions
            .Where(v => v.TemplateId == templateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct);
        return (max ?? 0) + 1;
    }

    private async Task<EmailTemplate> FindOwnedAsync(Guid id, CancellationToken ct) =>
        await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id && t.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();

    private async Task EnsureTemplateOwnedAsync(Guid templateId, CancellationToken ct)
    {
        var exists = await db.EmailTemplates.AnyAsync(t => t.Id == templateId && t.UserId == currentUser.UserId, ct);
        if (!exists) throw new NotFoundException();
    }
}
