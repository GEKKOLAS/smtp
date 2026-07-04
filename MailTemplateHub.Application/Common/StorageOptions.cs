using System.ComponentModel.DataAnnotations;

namespace MailTemplateHub.Application.Common;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required] public string ServiceUrl { get; init; } = string.Empty; // S3 API endpoint (MinIO in dev)
    [Required] public string AccessKey { get; init; } = string.Empty;
    [Required] public string SecretKey { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-1";

    /// <summary>MinIO and many S3-compatibles require path-style addressing.</summary>
    public bool ForcePathStyle { get; init; } = true;

    public string PrivateBucket { get; init; } = "mth-private";
    public string PublicBucket { get; init; } = "mth-public";
    public string SnapshotsBucket { get; init; } = "mth-snapshots";

    /// <summary>Base URL for public objects (public bucket or CDN), used to build public_url.</summary>
    [Required] public string PublicBaseUrl { get; init; } = string.Empty;

    public int UploadUrlExpiryMinutes { get; init; } = 10;
    public int DownloadUrlExpiryMinutes { get; init; } = 5;
}
