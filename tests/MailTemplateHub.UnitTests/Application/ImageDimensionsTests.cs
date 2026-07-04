using MailTemplateHub.Application.Features.Assets;

namespace MailTemplateHub.UnitTests.Application;

public class ImageDimensionsTests
{
    [Fact]
    public void Reads_png_dimensions_from_ihdr()
    {
        // 8-byte signature, 4-byte length, "IHDR", then width=2 height=3 (big-endian).
        var png = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        png[16] = 0; png[17] = 0; png[18] = 0; png[19] = 2; // width
        png[20] = 0; png[21] = 0; png[22] = 0; png[23] = 3; // height

        Assert.Equal((2, 3), ImageDimensions.Read(SignatureFamily.Png, png));
    }

    [Fact]
    public void Reads_gif_dimensions_little_endian()
    {
        var gif = new byte[10];
        "GIF89a"u8.CopyTo(gif);
        gif[6] = 0x0A; gif[7] = 0x00; // width = 10
        gif[8] = 0x14; gif[9] = 0x00; // height = 20

        Assert.Equal((10, 20), ImageDimensions.Read(SignatureFamily.Gif, gif));
    }

    [Fact]
    public void Returns_null_for_unsupported_family()
    {
        Assert.Null(ImageDimensions.Read(SignatureFamily.Pdf, "%PDF-1.4"u8));
    }
}
