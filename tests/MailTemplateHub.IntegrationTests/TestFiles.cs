namespace MailTemplateHub.IntegrationTests;

/// <summary>Minimal valid file byte payloads for upload tests.</summary>
public static class TestFiles
{
    // 1x1 red PNG.
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    // 1x1 transparent GIF (GIF89a).
    public static readonly byte[] Gif = Convert.FromBase64String(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    // Minimal PDF.
    public static readonly byte[] Pdf =
        "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();
}
