using System.Security.Cryptography;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Assets;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Ai;

public sealed record GenerateImageCommand(string Prompt, string? Size);

/// <summary>
/// Generates an image from a prompt (OpenAI) and stores it as a ready, public
/// asset — so it gets a stable URL usable in emails immediately, and shows up in
/// every asset picker. Mirrors the verify-and-persist half of the upload flow
/// (<see cref="CompleteUploadHandler"/>) but for server-produced bytes.
/// </summary>
public sealed class GenerateImageHandler(
    IAiImageGenerator generator,
    IAppDbContext db,
    ICurrentUser currentUser,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions,
    IOptions<AssetOptions> assetOptions,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<AssetDto> HandleAsync(GenerateImageCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Prompt) || command.Prompt.Length > 1000)
        {
            throw new ValidationFailureLite("prompt", "A prompt of up to 1000 characters is required.");
        }

        var generated = await generator.GenerateAsync(command.Prompt.Trim(), command.Size, ct);
        var bytes = generated.Bytes;

        // Defensive: the bytes must really be a PNG before we trust and store them.
        // Read signature + dimensions up front (spans can't cross the awaits below).
        var pngType = AllowedFileTypes.All.First(t => t.Mime == "image/png");
        var prefixLength = Math.Min(FileSignatureInspector.PrefixLength, bytes.Length);
        if (!FileSignatureInspector.Matches(pngType.Signature, bytes.AsSpan(0, prefixLength)))
        {
            throw new AiGenerationException("The generated image was not a valid PNG. Please try again.");
        }
        var dims = ImageDimensions.Read(pngType.Signature, bytes.AsSpan(0, prefixLength));

        var limits = assetOptions.Value;
        if (bytes.LongLength > limits.MaxImageBytes)
        {
            throw new AiGenerationException("The generated image exceeds the size limit.");
        }

        var usedBytes = await db.Assets
            .Where(a => a.UserId == currentUser.UserId && a.UploadState == AssetUploadState.Ready)
            .SumAsync(a => a.SizeBytes, ct);
        if (usedBytes + bytes.LongLength > limits.PerUserQuotaBytes)
        {
            throw new ConflictException("asset.quota_exceeded", "You have reached your storage quota.");
        }

        var options = storageOptions.Value;
        var assetId = Guid.CreateVersion7();
        var filename = $"ai-{assetId}.png";
        var storageKey = StorageKeys.For(currentUser.UserId, assetId, filename);
        var checksum = SHA256.HashData(bytes);

        // Store canonically in private, then publish a copy so the URL is stable.
        await storage.PutAsync(options.PrivateBucket, storageKey, bytes, generated.MimeType, ct);
        await storage.CopyAsync(options.PrivateBucket, storageKey, options.PublicBucket, storageKey, ct);

        var asset = new Asset
        {
            Id = assetId,
            UserId = currentUser.UserId,
            Kind = AssetKind.Image,
            OriginalFilename = filename,
            StorageKey = storageKey,
            MimeType = generated.MimeType,
            SizeBytes = bytes.LongLength,
            ChecksumSha256 = checksum,
            Width = dims?.Width,
            Height = dims?.Height,
            UploadState = AssetUploadState.Ready,
            Access = AssetAccess.Public,
            PublicUrl = $"{options.PublicBaseUrl.TrimEnd('/')}/{storageKey}",
        };
        db.Assets.Add(asset);

        audit.Add(AuditActions.AssetGenerated, currentUser.UserId, "asset", asset.Id,
            new { sizeBytes = bytes.LongLength });
        await db.SaveChangesAsync(ct);

        return AssetDto.From(asset);
    }
}
