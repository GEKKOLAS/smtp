namespace MailTemplateHub.Application.Abstractions.Storage;

public sealed record PresignedUpload(string Url, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

public sealed record ObjectHead(long SizeBytes, string? ContentType);

/// <summary>
/// S3-compatible object storage (MinIO in dev). Uploads bypass the API body
/// pipeline: the API issues a presigned PUT, the browser uploads directly, then
/// the API reads the object back to verify it (spec 03 §1, 04 §4).
/// </summary>
public interface IObjectStorage
{
    Task<PresignedUpload> CreatePresignedUploadAsync(
        string bucket, string key, string contentType, TimeSpan expiry, CancellationToken ct);

    Task<string> CreatePresignedDownloadUrlAsync(
        string bucket, string key, TimeSpan expiry, CancellationToken ct);

    Task<ObjectHead?> HeadAsync(string bucket, string key, CancellationToken ct);

    /// <summary>Opens the object for reading (used to verify magic bytes and checksum).</summary>
    Task<Stream> OpenReadAsync(string bucket, string key, CancellationToken ct);

    /// <summary>Writes bytes directly from the server (e.g. rendered snapshots).</summary>
    Task PutAsync(string bucket, string key, byte[] content, string contentType, CancellationToken ct);

    Task CopyAsync(string sourceBucket, string sourceKey, string destBucket, string destKey, CancellationToken ct);

    Task DeleteAsync(string bucket, string key, CancellationToken ct);
}
