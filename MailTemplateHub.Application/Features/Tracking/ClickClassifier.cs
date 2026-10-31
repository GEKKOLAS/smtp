using System.Text.RegularExpressions;
using MailTemplateHub.Domain.Entities;

namespace MailTemplateHub.Application.Features.Tracking;

/// <summary>
/// Separates people from link scanners. Mail security gateways (Outlook Safe
/// Links, Proofpoint, Mimecast…) open links before the recipient sees them;
/// counting those would inflate every click rate (spec 14 F4.2).
/// </summary>
public static partial class ClickClassifier
{
    public static string Classify(string? userAgent, DateTimeOffset? deliveredAt, DateTimeOffset now, TimeSpan window)
    {
        if (string.IsNullOrWhiteSpace(userAgent) || ScannerRegex().IsMatch(userAgent))
        {
            return ClickClassifications.Scanner;
        }
        // Nobody reads and clicks within seconds of delivery; gateways do.
        if (deliveredAt is { } delivered && now - delivered < window)
        {
            return ClickClassifications.Scanner;
        }
        return ClickClassifications.Human;
    }

    [GeneratedRegex(
        @"safelinks|microsoft office|ms-office|bingpreview|proofpoint|mimecast|barracuda|symantec|trend ?micro|"
        + @"forcepoint|fortiguard|sophos|zscaler|ironport|cisco|python-requests|python-urllib|curl/|wget/|"
        + @"go-http-client|okhttp|java/|headlesschrome|phantomjs|\bbot\b|bot/|crawler|spider",
        RegexOptions.IgnoreCase)]
    private static partial Regex ScannerRegex();
}
