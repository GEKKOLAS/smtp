using MailTemplateHub.Application.Features.Assets;

namespace MailTemplateHub.UnitTests.Application;

public class FileSignatureInspectorTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];
    private static readonly byte[] Gif = "GIF89a"u8.ToArray();
    private static readonly byte[] Pdf = "%PDF-1.7"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0];

    [Fact]
    public void Accepts_matching_signatures()
    {
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Png, Png));
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Jpeg, Jpeg));
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Gif, Gif));
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Pdf, Pdf));
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Zip, Zip));
    }

    [Fact]
    public void Rejects_mismatched_signatures()
    {
        // PDF bytes declared as PNG must fail (the stored-XSS / spoofing guard).
        Assert.False(FileSignatureInspector.Matches(SignatureFamily.Png, Pdf));
        Assert.False(FileSignatureInspector.Matches(SignatureFamily.Gif, Png));
        Assert.False(FileSignatureInspector.Matches(SignatureFamily.Zip, Jpeg));
    }

    [Fact]
    public void Webp_requires_riff_and_webp_markers()
    {
        var webp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 };
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Webp, webp));

        var riffOnly = new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.False(FileSignatureInspector.Matches(SignatureFamily.Webp, riffOnly));
    }

    [Fact]
    public void Text_rejects_content_with_nul_bytes()
    {
        Assert.True(FileSignatureInspector.Matches(SignatureFamily.Text, "hello,world"u8));
        Assert.False(FileSignatureInspector.Matches(SignatureFamily.Text, [0x68, 0x00, 0x69]));
    }
}
