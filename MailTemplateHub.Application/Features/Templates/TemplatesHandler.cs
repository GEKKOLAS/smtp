using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Templates;

public sealed record CreateTemplateCommand(string Name, string? Description, TemplateContentInput Content);
public sealed record UpdateTemplateCommand(string? Name, string? Description);
public sealed record TemplateListQuery(string? Search, bool Archived, int Page, int PageSize);
public sealed record PagedTemplates(IReadOnlyList<TemplateSummaryDto> Items, int Page, int PageSize, int TotalCount);

public sealed class TemplatesHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    TemplateVersionFactory versionFactory,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<PagedTemplates> ListAsync(TemplateListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.EmailTemplates.Where(t => t.UserId == currentUser.UserId && t.IsArchived == query.Archived);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(t => t.Name.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TemplateSummaryDto(
                t.Id, t.Name, t.Description, t.IsArchived,
                t.CurrentVersion == null ? (int?)null : t.CurrentVersion.VersionNumber,
                t.UpdatedAt))
            .ToListAsync(ct);

        return new PagedTemplates(items, page, pageSize, total);
    }

    public async Task<TemplateDto> GetAsync(Guid id, CancellationToken ct)
    {
        var template = await db.EmailTemplates
            .Include(t => t.CurrentVersion!).ThenInclude(v => v.TemplateAssets)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        return TemplateDto.From(template);
    }

    public async Task<TemplateDto> CreateAsync(CreateTemplateCommand command, CancellationToken ct)
    {
        ValidateName(command.Name);
        await EnsureNameAvailableAsync(command.Name, excludeId: null, ct);

        var template = new EmailTemplate
        {
            UserId = currentUser.UserId,
            Name = command.Name.Trim(),
            Description = command.Description,
        };
        var version = await versionFactory.BuildAsync(template.Id, 1, command.Content, ct);
        await PersistWithCurrentVersionAsync(template, version, ct);

        audit.Add(AuditActions.TemplateCreated, currentUser.UserId, "email_template", template.Id);
        await db.SaveChangesAsync(ct);

        return await GetAsync(template.Id, ct);
    }

    public async Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateCommand command, CancellationToken ct)
    {
        var template = await FindOwnedAsync(id, ct);

        if (command.Name is not null)
        {
            ValidateName(command.Name);
            await EnsureNameAvailableAsync(command.Name, excludeId: id, ct);
            template.Name = command.Name.Trim();
        }
        if (command.Description is not null) template.Description = command.Description;

        audit.Add(AuditActions.TemplateUpdated, currentUser.UserId, "email_template", id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TemplateDto> DuplicateAsync(Guid id, CancellationToken ct)
    {
        var source = await db.EmailTemplates
            .Include(t => t.CurrentVersion!).ThenInclude(v => v.TemplateAssets)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        if (source.CurrentVersion is null) throw new NotFoundException();

        var copyName = await UniqueCopyNameAsync(source.Name, ct);
        var copy = new EmailTemplate
        {
            UserId = currentUser.UserId,
            Name = copyName,
            Description = source.Description,
        };
        var version = CloneVersion(source.CurrentVersion, copy.Id, 1);
        await PersistWithCurrentVersionAsync(copy, version, ct);

        audit.Add(AuditActions.TemplateDuplicated, currentUser.UserId, "email_template", copy.Id,
            new { sourceId = id });
        await db.SaveChangesAsync(ct);
        return await GetAsync(copy.Id, ct);
    }

    public async Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct)
    {
        var template = await FindOwnedAsync(id, ct);
        template.IsArchived = archived;
        audit.Add(AuditActions.TemplateArchived, currentUser.UserId, "email_template", id, new { archived });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var template = await FindOwnedAsync(id, ct);
        template.DeletedAt = clock.UtcNow; // soft delete; versions survive for history
        audit.Add(AuditActions.TemplateDeleted, currentUser.UserId, "email_template", id);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    /// <summary>
    /// Inserts a new template and its first version, then points the template at
    /// that version in a second save. Two saves avoid the template↔version FK
    /// cycle (each references the other's id).
    /// </summary>
    private async Task PersistWithCurrentVersionAsync(
        EmailTemplate template, EmailTemplateVersion version, CancellationToken ct)
    {
        db.EmailTemplates.Add(template);
        db.EmailTemplateVersions.Add(version);
        await db.SaveChangesAsync(ct);

        template.CurrentVersionId = version.Id;
    }

    internal static EmailTemplateVersion CloneVersion(EmailTemplateVersion source, Guid templateId, int versionNumber) =>
        new()
        {
            TemplateId = templateId,
            VersionNumber = versionNumber,
            Subject = source.Subject,
            Preheader = source.Preheader,
            MjmlSource = source.MjmlSource,
            GrapesProject = source.GrapesProject is null
                ? null
                : System.Text.Json.JsonDocument.Parse(source.GrapesProject.RootElement.GetRawText()),
            HtmlBody = source.HtmlBody,
            TextBody = source.TextBody,
            VariablesSchema = System.Text.Json.JsonDocument.Parse(source.VariablesSchema.RootElement.GetRawText()),
            EditorKind = source.EditorKind,
            CreatedByUserId = source.CreatedByUserId,
            TemplateAssets = source.TemplateAssets
                .Select(a => new TemplateAsset { AssetId = a.AssetId, Usage = a.Usage, ContentId = a.ContentId })
                .ToList(),
        };

    private async Task<EmailTemplate> FindOwnedAsync(Guid id, CancellationToken ct) =>
        await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id && t.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ValidationFailure("name", "Name is required and must be 200 characters or fewer.");
        }
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var taken = await db.EmailTemplates.AnyAsync(
            t => t.UserId == currentUser.UserId
                 && t.Id != excludeId
                 && t.Name.ToLower() == normalized, ct);
        if (taken) throw new ConflictException("template.name_taken", "A template with this name already exists.");
    }

    private async Task<string> UniqueCopyNameAsync(string sourceName, CancellationToken ct)
    {
        var baseName = $"Copy of {sourceName}";
        var candidate = baseName;
        var n = 2;
        while (await db.EmailTemplates.AnyAsync(
                   t => t.UserId == currentUser.UserId && t.Name.ToLower() == candidate.ToLower(), ct))
        {
            candidate = $"{baseName} ({n++})";
            if (candidate.Length > 200) candidate = candidate[^200..];
        }
        return candidate;
    }
}
