namespace MailTemplateHub.Application.Features.Assets;

/// <summary>
/// Verifies that a file's leading bytes match its declared type (spec 04 §4).
/// Pure logic over a byte prefix, so it is unit-tested without storage.
/// </summary>
public static class FileSignatureInspector
{
    /// <summary>
    /// Header bytes captured for signature + dimension checks. Larger than any
    /// signature so image dimension headers (PNG IHDR, JPEG SOF) fit too.
    /// </summary>
    public const int PrefixLength = 512;

    public static bool Matches(SignatureFamily family, ReadOnlySpan<byte> prefix) => family switch
    {
        SignatureFamily.Png => StartsWith(prefix, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        SignatureFamily.Jpeg => StartsWith(prefix, [0xFF, 0xD8, 0xFF]),
        SignatureFamily.Gif => StartsWith(prefix, "GIF87a"u8) || StartsWith(prefix, "GIF89a"u8),
        SignatureFamily.Webp => prefix.Length >= 12
            && StartsWith(prefix, "RIFF"u8)
            && prefix.Slice(8, 4).SequenceEqual("WEBP"u8),
        SignatureFamily.Pdf => StartsWith(prefix, "%PDF-"u8),
        SignatureFamily.Zip => StartsWith(prefix, [0x50, 0x4B, 0x03, 0x04])
            || StartsWith(prefix, [0x50, 0x4B, 0x05, 0x06])  // empty archive
            || StartsWith(prefix, [0x50, 0x4B, 0x07, 0x08]), // spanned
        SignatureFamily.Text => IsProbablyText(prefix),
        _ => false,
    };

    private static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) =>
        data.Length >= signature.Length && data[..signature.Length].SequenceEqual(signature);

    // Text has no signature; reject only clear binary markers (NUL bytes).
    private static bool IsProbablyText(ReadOnlySpan<byte> prefix)
    {
        foreach (var b in prefix)
        {
            if (b == 0) return false;
        }
        return true;
    }
}
