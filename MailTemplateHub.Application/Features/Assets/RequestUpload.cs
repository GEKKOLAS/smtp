using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Assets;

public sealed record RequestUploadCommand(string Filename, string MimeType, long SizeBytes);

public sealed class RequestUploadValidator : AbstractValidator<RequestUploadCommand>
{
    public RequestUploadValidator()
    {
        RuleFor(x => x.Filename).NotEmpty().MaximumLength(255);
        RuleFor(x => x.MimeType).NotEmpty()
            .Must(AllowedFileTypes.IsAllowed)
            .WithErrorCode("asset.type_not_allowed")
            .WithMessage("This file type is not allowed.");
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

/// <summary>
/// Phase 1 of upload: validates the declared file, reserves an Asset row, and
/// returns a presigned PUT to the private bucket (spec 03 §1, 04 §4, 06 §7).
/// </summary>
public sealed class RequestUploadHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions,
    IOptions<AssetOptions> assetOptions,
    IValidator<RequestUploadCommand> validator,
    IClock clock)
{
    public async Task<UploadGrantDto> HandleAsync(RequestUploadCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var type = AllowedFileTypes.All.First(t =>
            string.Equals(t.Mime, command.MimeType, StringComparison.OrdinalIgnoreCase));

        var limits = assetOptions.Value;
        var maxBytes = AllowedFileTypes.IsImageKind(type.Kind) ? limits.MaxImageBytes : limits.MaxFileBytes;
        if (command.SizeBytes > maxBytes)
        {
            throw new Common.ConflictException("asset.too_large",
                $"The file exceeds the {maxBytes / (1024 * 1024)} MB limit.");
        }

        // Best-effort quota check against the declared size; the actual size is
        // re-verified on complete.
        var usedBytes = await db.Assets
            .Where(a => a.UserId == currentUser.UserId && a.UploadState == AssetUploadState.Ready)
            .SumAsync(a => a.SizeBytes, ct);
        if (usedBytes + command.SizeBytes > limits.PerUserQuotaBytes)
        {
            throw new Common.ConflictException("asset.quota_exceeded", "You have reached your storage quota.");
        }

        var options = storageOptions.Value;
        var assetId = Guid.CreateVersion7();
        var storageKey = StorageKeys.For(currentUser.UserId, assetId, command.Filename);

        var asset = new Asset
        {
            Id = assetId,
            UserId = currentUser.UserId,
            Kind = type.Kind,
            OriginalFilename = StorageKeys.Sanitize(command.Filename),
            StorageKey = storageKey,
            MimeType = type.Mime,
            SizeBytes = command.SizeBytes,
            UploadState = AssetUploadState.Pending,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync(ct);

        var presign = await storage.CreatePresignedUploadAsync(
            options.PrivateBucket, storageKey, type.Mime,
            TimeSpan.FromMinutes(options.UploadUrlExpiryMinutes), ct);

        return new UploadGrantDto(assetId, presign.Url, presign.Headers, presign.ExpiresAt);
    }
}
