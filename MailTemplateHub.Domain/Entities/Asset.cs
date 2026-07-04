using MailTemplateHub.Domain.Common;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// A user-owned uploaded file (image/GIF/document/…), stored in object storage
/// with metadata here (spec 05-database.md). Assets are stored separately from
/// template metadata and referenced by stable keys.
/// </summary>
public sealed class Asset : IHasTimestamps, ISoftDeletable
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required AssetKind Kind { get; set; }
    public required string OriginalFilename { get; set; }

    /// <summary>Server-generated object key: assets/{userId}/{assetId}/{safeName}.</summary>
    public required string StorageKey { get; init; }

    public string? PublicUrl { get; set; }
    public AssetAccess Access { get; set; } = AssetAccess.Private;
    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public byte[]? ChecksumSha256 { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public AssetUploadState UploadState { get; set; } = AssetUploadState.Pending;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
