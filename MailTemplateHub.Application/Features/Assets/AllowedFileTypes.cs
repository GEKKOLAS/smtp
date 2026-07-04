using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Features.Assets;

/// <summary>Signature family a MIME type must match when the object is verified.</summary>
public enum SignatureFamily
{
    Png,
    Jpeg,
    Gif,
    Webp,
    Pdf,
    Zip,   // docx/xlsx/pptx/zip are all zip containers
    Text,  // no reliable magic; validated as text (no NUL bytes)
}

public sealed record AllowedType(string Mime, string Extension, AssetKind Kind, SignatureFamily Signature);

/// <summary>
/// MVP allowlist (spec 01-prd §US-AST-1). SVG is intentionally excluded — it can
/// carry scripts and is never served as a hosted image.
/// </summary>
public static class AllowedFileTypes
{
    public static readonly IReadOnlyList<AllowedType> All =
    [
        new("image/png", "png", AssetKind.Image, SignatureFamily.Png),
        new("image/jpeg", "jpg", AssetKind.Image, SignatureFamily.Jpeg),
        new("image/webp", "webp", AssetKind.Image, SignatureFamily.Webp),
        new("image/gif", "gif", AssetKind.Gif, SignatureFamily.Gif),
        new("application/pdf", "pdf", AssetKind.Document, SignatureFamily.Pdf),
        new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx", AssetKind.Document, SignatureFamily.Zip),
        new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx", AssetKind.Document, SignatureFamily.Zip),
        new("application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx", AssetKind.Document, SignatureFamily.Zip),
        new("text/plain", "txt", AssetKind.Document, SignatureFamily.Text),
        new("text/csv", "csv", AssetKind.Document, SignatureFamily.Text),
        new("application/zip", "zip", AssetKind.Archive, SignatureFamily.Zip),
    ];

    private static readonly Dictionary<string, AllowedType> ByMime =
        All.ToDictionary(t => t.Mime, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string mime, out AllowedType type) => ByMime.TryGetValue(mime, out type!);

    public static bool IsAllowed(string mime) => ByMime.ContainsKey(mime);

    public static bool IsImageKind(AssetKind kind) => kind is AssetKind.Image or AssetKind.Gif;
}
