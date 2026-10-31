namespace MailTemplateHub.Application.Common;

/// <summary>Click tracking (spec 14 F4.1).</summary>
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    /// <summary>
    /// Public origin recipients' clicks go through, e.g. https://mail.example.com.
    /// Empty turns click tracking off: recipients can't reach localhost.
    /// </summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>Clicks older than this are deleted by the cleanup job.</summary>
    public int RetentionDays { get; init; } = 365;

    /// <summary>Clicks this soon after delivery are treated as link scanners, not people.</summary>
    public int ScannerWindowSeconds { get; init; } = 10;

    public bool IsConfigured =>
        Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public string RedirectUrl(string token) => $"{PublicBaseUrl.TrimEnd('/')}/api/v1/t/{token}";
}
