using System.Security.Cryptography;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Assets;

/// <summary>
/// Phase 2 of upload: reads the uploaded object back and verifies it — magic
/// bytes vs. declared MIME, actual size within limits, SHA-256 checksum, and
/// dedupe (spec 04 §4, 06 §7). On failure the object is deleted and the row rejected.
/// </summary>
public sealed class CompleteUploadHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions,
    IOptions<AssetOptions> assetOptions,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<AssetDto> HandleAsync(Guid assetId, CancellationToken ct)
    {
        var asset = await db.Assets
            .FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        if (asset.UploadState == AssetUploadState.Ready) return AssetDto.From(asset); // idempotent replay
        if (asset.UploadState == AssetUploadState.Rejected)
            throw new ConflictException("asset.rejected", "This upload was rejected.");

        var bucket = storageOptions.Value.PrivateBucket;
        var head = await storage.HeadAsync(bucket, asset.StorageKey, ct)
            ?? throw new UnprocessableAssetException("asset.not_uploaded", "No uploaded object was found.");

        AllowedFileTypes.TryGet(asset.MimeType, out var type);
        var maxBytes = AllowedFileTypes.IsImageKind(asset.Kind)
            ? assetOptions.Value.MaxImageBytes
            : assetOptions.Value.MaxFileBytes;

        var (checksum, actualSize, prefix) = await ReadAndHashAsync(bucket, asset.StorageKey, maxBytes, ct);

        if (checksum is null || actualSize > maxBytes)
        {
            await RejectAsync(asset, bucket, "asset.too_large", "The uploaded file exceeds the size limit.", ct);
        }

        if (!FileSignatureInspector.Matches(type.Signature, prefix))
        {
            await RejectAsync(asset, bucket, "asset.verification_failed",
                "The file content does not match its declared type.", ct);
        }

        // Dedupe: reuse an identical file already owned by this user.
        var existing = await db.Assets.FirstOrDefaultAsync(
            a => a.UserId == currentUser.UserId
                 && a.UploadState == AssetUploadState.Ready
                 && a.ChecksumSha256 == checksum,
            ct);
        if (existing is not null)
        {
            await storage.DeleteAsync(bucket, asset.StorageKey, ct);
            db.Assets.Remove(asset);
            await db.SaveChangesAsync(ct);
            return AssetDto.From(existing);
        }

        var dims = ImageDimensions.Read(type.Signature, prefix);
        asset.SizeBytes = actualSize;
        asset.ChecksumSha256 = checksum;
        asset.Width = dims?.Width;
        asset.Height = dims?.Height;
        asset.UploadState = AssetUploadState.Ready;

        audit.Add(AuditActions.AssetUploaded, currentUser.UserId, "asset", asset.Id,
            new { kind = asset.Kind.ToString().ToLowerInvariant(), sizeBytes = actualSize });
        await db.SaveChangesAsync(ct);

        return AssetDto.From(asset);
    }

    /// <summary>Streams the object once, computing the checksum and capturing the prefix.</summary>
    private async Task<(byte[]? Checksum, long Size, byte[] Prefix)> ReadAndHashAsync(
        string bucket, string key, long maxBytes, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(bucket, key, ct);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var prefix = new byte[FileSignatureInspector.PrefixLength];
        var prefixFilled = 0;
        long total = 0;

        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            hasher.AppendData(buffer, 0, read);
            total += read;

            if (prefixFilled < prefix.Length)
            {
                var copy = Math.Min(prefix.Length - prefixFilled, read);
                Array.Copy(buffer, 0, prefix, prefixFilled, copy);
                prefixFilled += copy;
            }

            if (total > maxBytes)
            {
                return (null, total, prefix[..prefixFilled]); // oversized; caller rejects
            }
        }

        // Trim to the bytes actually present so signature/text checks and the
        // dimension reader never see zero padding.
        return (hasher.GetHashAndReset(), total, prefix[..prefixFilled]);
    }

    private async Task RejectAsync(Asset asset, string bucket, string code, string message, CancellationToken ct)
    {
        await storage.DeleteAsync(bucket, asset.StorageKey, ct);
        asset.UploadState = AssetUploadState.Rejected;
        audit.Add(AuditActions.AssetRejected, currentUser.UserId, "asset", asset.Id, new { reason = code });
        await db.SaveChangesAsync(ct);
        throw new UnprocessableAssetException(code, message);
    }
}

/// <summary>Verification failed (maps to 422).</summary>
public sealed class UnprocessableAssetException(string code, string message) : AppException(code, message);
