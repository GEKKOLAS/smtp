using MailTemplateHub.Domain.Entities;

namespace MailTemplateHub.Application.Features.Assets;

public sealed record AssetDto(
    Guid Id,
    string Kind,
    string OriginalFilename,
    string MimeType,
    long SizeBytes,
    string Access,
    string? PublicUrl,
    int? Width,
    int? Height,
    string? ChecksumSha256,
    DateTimeOffset CreatedAt)
{
    public static AssetDto From(Asset a) => new(
        a.Id,
        a.Kind.ToString().ToLowerInvariant(),
        a.OriginalFilename,
        a.MimeType,
        a.SizeBytes,
        a.Access.ToString().ToLowerInvariant(),
        a.PublicUrl,
        a.Width,
        a.Height,
        a.ChecksumSha256 is null ? null : Convert.ToHexString(a.ChecksumSha256).ToLowerInvariant(),
        a.CreatedAt);
}

public sealed record UploadGrantDto(
    Guid AssetId, string UploadUrl, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

public sealed record AssetUsageDto(Guid TemplateId, string TemplateName, int VersionNumber);
