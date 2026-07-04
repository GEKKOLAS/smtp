using Amazon.S3;
using Amazon.S3.Model;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage (MinIO in dev) via AWSSDK.S3. Presigned URLs let
/// the browser upload/download directly without proxying bytes through the API.
/// </summary>
internal sealed class S3ObjectStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IObjectStorage
{
    // AWSSDK v4 always presigns as https even for http endpoints. The scheme is
    // not part of the SigV4 signature (only the host is), so we can safely swap
    // it back to http for a plain-HTTP endpoint like local MinIO.
    private readonly bool _useHttp =
        options.Value.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    public Task<PresignedUpload> CreatePresignedUploadAsync(
        string bucket, string key, string contentType, TimeSpan expiry, CancellationToken ct)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType,
        };
        var url = NormalizeScheme(s3.GetPreSignedURL(request));
        var headers = new Dictionary<string, string> { ["Content-Type"] = contentType };
        return Task.FromResult(new PresignedUpload(url, headers, DateTimeOffset.UtcNow.Add(expiry)));
    }

    public Task<string> CreatePresignedDownloadUrlAsync(
        string bucket, string key, TimeSpan expiry, CancellationToken ct)
    {
        var url = NormalizeScheme(s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
        }));
        return Task.FromResult(url);
    }

    private string NormalizeScheme(string url) =>
        _useHttp && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("http://", url.AsSpan("https://".Length))
            : url;

    public async Task<ObjectHead?> HeadAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectMetadataAsync(bucket, key, ct);
            return new ObjectHead(response.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Stream> OpenReadAsync(string bucket, string key, CancellationToken ct)
    {
        var response = await s3.GetObjectAsync(bucket, key, ct);
        return response.ResponseStream;
    }

    public async Task CopyAsync(
        string sourceBucket, string sourceKey, string destBucket, string destKey, CancellationToken ct)
    {
        await s3.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = sourceBucket,
            SourceKey = sourceKey,
            DestinationBucket = destBucket,
            DestinationKey = destKey,
        }, ct);
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(bucket, key, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Deleting a missing object is a no-op.
        }
    }
}
