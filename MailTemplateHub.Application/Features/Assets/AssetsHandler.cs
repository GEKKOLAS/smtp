using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Assets;

public sealed record AssetListQuery(string? Kind, string? Search, int Page, int PageSize);

public sealed record PagedAssets(IReadOnlyList<AssetDto> Items, int Page, int PageSize, int TotalCount);

public sealed record DownloadUrlDto(string Url, DateTimeOffset ExpiresAt);

public sealed class AssetsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<PagedAssets> ListAsync(AssetListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Assets.Where(a =>
            a.UserId == currentUser.UserId && a.UploadState == AssetUploadState.Ready);

        if (query.Kind is not null && Enum.TryParse<AssetKind>(query.Kind, ignoreCase: true, out var kind))
        {
            q = q.Where(a => a.Kind == kind);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Case-insensitive contains; provider-agnostic (translates to LOWER() LIKE).
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(a => a.OriginalFilename.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedAssets(items.Select(AssetDto.From).ToList(), page, pageSize, total);
    }

    public async Task<AssetDto> GetAsync(Guid id, CancellationToken ct)
        => AssetDto.From(await FindReadyAsync(id, ct));

    public async Task<DownloadUrlDto> GetDownloadUrlAsync(Guid id, CancellationToken ct)
    {
        var asset = await FindReadyAsync(id, ct);
        var options = storageOptions.Value;

        if (asset.Access == AssetAccess.Public && asset.PublicUrl is not null)
        {
            return new DownloadUrlDto(asset.PublicUrl, clock.UtcNow.AddYears(1));
        }

        var expiry = TimeSpan.FromMinutes(options.DownloadUrlExpiryMinutes);
        var url = await storage.CreatePresignedDownloadUrlAsync(options.PrivateBucket, asset.StorageKey, expiry, ct);
        return new DownloadUrlDto(url, clock.UtcNow.Add(expiry));
    }

    public async Task<AssetDto> SetVisibilityAsync(Guid id, AssetAccess access, CancellationToken ct)
    {
        var asset = await FindReadyAsync(id, ct);
        var options = storageOptions.Value;

        if (access == AssetAccess.Public && !AllowedFileTypes.IsImageKind(asset.Kind))
        {
            throw new ConflictException("asset.not_publishable", "Only images and GIFs can be made public.");
        }

        if (access == asset.Access) return AssetDto.From(asset);

        if (access == AssetAccess.Public)
        {
            // Add a served copy in the public bucket; private stays canonical.
            await storage.CopyAsync(options.PrivateBucket, asset.StorageKey, options.PublicBucket, asset.StorageKey, ct);
            asset.PublicUrl = $"{options.PublicBaseUrl.TrimEnd('/')}/{asset.StorageKey}";
            asset.Access = AssetAccess.Public;
        }
        else
        {
            await storage.DeleteAsync(options.PublicBucket, asset.StorageKey, ct);
            asset.PublicUrl = null;
            asset.Access = AssetAccess.Private;
        }

        await db.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }

    public async Task DeleteAsync(Guid id, bool force, CancellationToken ct)
    {
        var asset = await db.Assets
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        // Block deletion of an asset still referenced by a live template version,
        // unless the caller forces it (spec 06 §7).
        if (!force)
        {
            var usages = await db.TemplateAssets
                .Where(ta => ta.AssetId == id
                             && db.EmailTemplateVersions.Any(v =>
                                 v.Id == ta.TemplateVersionId && v.Template!.DeletedAt == null))
                .Select(ta => new AssetUsageDto(
                    ta.TemplateVersionId,
                    db.EmailTemplateVersions
                        .Where(v => v.Id == ta.TemplateVersionId)
                        .Select(v => v.Template!.Name).First(),
                    db.EmailTemplateVersions
                        .Where(v => v.Id == ta.TemplateVersionId)
                        .Select(v => v.VersionNumber).First()))
                .Distinct()
                .ToListAsync(ct);
            if (usages.Count > 0) throw new AssetInUseException(usages);
        }

        var options = storageOptions.Value;
        await storage.DeleteAsync(options.PrivateBucket, asset.StorageKey, ct);
        if (asset.Access == AssetAccess.Public)
        {
            await storage.DeleteAsync(options.PublicBucket, asset.StorageKey, ct);
        }

        asset.DeletedAt = clock.UtcNow;
        audit.Add(AuditActions.AssetDeleted, currentUser.UserId, "asset", asset.Id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Asset> FindReadyAsync(Guid id, CancellationToken ct) =>
        await db.Assets.FirstOrDefaultAsync(
            a => a.Id == id && a.UserId == currentUser.UserId && a.UploadState == AssetUploadState.Ready, ct)
        ?? throw new NotFoundException();
}

/// <summary>Asset still referenced by a template version (maps to 409 with usages).</summary>
public sealed class AssetInUseException(IReadOnlyList<AssetUsageDto> usages)
    : ConflictException("asset.in_use", "This asset is used by one or more templates.")
{
    public IReadOnlyList<AssetUsageDto> Usages { get; } = usages;
}
