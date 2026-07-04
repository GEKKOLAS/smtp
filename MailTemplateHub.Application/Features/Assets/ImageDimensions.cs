using System.Buffers.Binary;

namespace MailTemplateHub.Application.Features.Assets;

/// <summary>
/// Minimal width/height reader for PNG, GIF, and JPEG headers — enough for the
/// media library, without pulling in an imaging dependency. WEBP and others
/// return null (dimensions are optional metadata).
/// </summary>
public static class ImageDimensions
{
    public static (int Width, int Height)? Read(SignatureFamily family, ReadOnlySpan<byte> header) => family switch
    {
        SignatureFamily.Png => ReadPng(header),
        SignatureFamily.Gif => ReadGif(header),
        SignatureFamily.Jpeg => ReadJpeg(header),
        _ => null,
    };

    private static (int, int)? ReadPng(ReadOnlySpan<byte> h)
    {
        // IHDR width/height are big-endian 4-byte ints at offset 16 and 20.
        if (h.Length < 24) return null;
        var width = BinaryPrimitives.ReadInt32BigEndian(h.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(h.Slice(20, 4));
        return width > 0 && height > 0 ? (width, height) : null;
    }

    private static (int, int)? ReadGif(ReadOnlySpan<byte> h)
    {
        // Logical screen width/height are little-endian 2-byte ints at offset 6 and 8.
        if (h.Length < 10) return null;
        var width = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(6, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(8, 2));
        return width > 0 && height > 0 ? (width, height) : null;
    }

    private static (int, int)? ReadJpeg(ReadOnlySpan<byte> h)
    {
        // Scan segments for a Start-Of-Frame marker carrying height/width.
        var i = 2; // skip SOI (FF D8)
        while (i + 9 < h.Length)
        {
            if (h[i] != 0xFF) { i++; continue; }
            var marker = h[i + 1];
            // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15
            if (marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                var height = BinaryPrimitives.ReadUInt16BigEndian(h.Slice(i + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(h.Slice(i + 7, 2));
                return width > 0 && height > 0 ? (width, height) : null;
            }
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(h.Slice(i + 2, 2));
            i += 2 + segmentLength;
        }
        return null;
    }
}
