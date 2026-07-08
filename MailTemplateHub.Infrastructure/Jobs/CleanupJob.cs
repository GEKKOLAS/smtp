using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Jobs;

/// <summary>
/// Daily storage/data hygiene sweep (spec 10-jobs.md J5): abandoned uploads,
/// expired transient rows, and unreferenced soft-deleted assets.
/// </summary>
public sealed class CleanupJob(
    IAppDbContext db,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions,
    IClock clock,
    ILogger<CleanupJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var buckets = storageOptions.Value;

        // (a) Abandoned pending uploads older than 24h.
        var stalePending = await db.Assets
            .IgnoreQueryFilters()
            .Where(a => a.UploadState == AssetUploadState.Pending && a.CreatedAt < now.AddHours(-24))
            .ToListAsync(ct);
        foreach (var asset in stalePending)
        {
            await SafeDeleteAsync(buckets.PrivateBucket, asset.StorageKey, ct);
            db.Assets.Remove(asset);
        }

        // (b) Expired transient rows.
        await db.OAuthStates.Where(s => s.ExpiresAt < now).ExecuteDeleteAsync(ct);
        await db.PasswordResetTokens.Where(t => t.ExpiresAt < now || t.UsedAt != null).ExecuteDeleteAsync(ct);
        await db.UserSessions.Where(s => s.ExpiresAt < now).ExecuteDeleteAsync(ct);

        // (c) Soft-deleted assets no longer referenced by any template version or send.
        var softDeleted = await db.Assets
            .IgnoreQueryFilters()
            .Where(a => a.DeletedAt != null)
            .ToListAsync(ct);
        int purged = 0;
        foreach (var asset in softDeleted)
        {
            var referenced =
                await db.TemplateAssets.AnyAsync(ta => ta.AssetId == asset.Id, ct)
                || await db.EmailSendAttachments.AnyAsync(sa => sa.AssetId == asset.Id, ct);
            if (referenced) continue;

            await SafeDeleteAsync(buckets.PrivateBucket, asset.StorageKey, ct);
            if (asset.Access == AssetAccess.Public)
            {
                await SafeDeleteAsync(buckets.PublicBucket, asset.StorageKey, ct);
            }
            db.Assets.Remove(asset);
            purged++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Cleanup: {Pending} abandoned uploads, {Purged} deleted assets removed",
            stalePending.Count, purged);
    }

    private async Task SafeDeleteAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await storage.DeleteAsync(bucket, key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cleanup failed to delete {Bucket}/{Key}", bucket, key);
        }
    }
}
