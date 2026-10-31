using System.Net;
using System.Text.RegularExpressions;

namespace MailTemplateHub.Application.Features.Tracking;

public sealed record RewrittenLink(string OriginalUrl, string TargetUrl, int Position);

public sealed record LinkRewriteResult(string Html, string Text, IReadOnlyList<RewrittenLink> Links);

/// <summary>
/// Rewrites the links of one rendered email for UTM tagging and click tracking
/// (spec 14 F4.1). Runs after rendering, so hrefs are final and sanitized.
/// Unsubscribe links, mailto:, tel:, anchors and cid: are never touched.
/// </summary>
public static partial class LinkRewriter
{
    private const string UtmSource = "mail-template-hub";
    private const string UtmMedium = "email";

    /// <param name="utmCampaign">Adds UTM tags when set.</param>
    /// <param name="track">
    /// Maps a target URL and its position to the URL emitted in the email (the
    /// redirect), or null when the send isn't tracked.
    /// </param>
    public static LinkRewriteResult Rewrite(
        string html, string text, string? utmCampaign, Func<string, int, string>? track)
    {
        var links = new List<RewrittenLink>();
        var emitted = new Dictionary<string, string>(StringComparer.Ordinal);

        var rewrittenHtml = AnchorRegex().Replace(html, match =>
        {
            var original = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            if (!IsHttpUrl(original) || IsUnsubscribe(original, match.Groups["inner"].Value)) return match.Value;

            var target = utmCampaign is null ? original : AddUtm(original, utmCampaign);
            if (!emitted.TryGetValue(original, out var url))
            {
                var position = links.Count;
                url = track?.Invoke(target, position) ?? target;
                emitted[original] = url;
                links.Add(new RewrittenLink(original, target, position));
            }

            var quote = match.Groups["q"].Value;
            return $"<a{match.Groups["before"].Value}href={quote}{WebUtility.HtmlEncode(url)}{quote}"
                   + $"{match.Groups["after"].Value}>{match.Groups["inner"].Value}</a>";
        });

        // The plain-text part carries the same links as bare URLs. Longest first,
        // so https://a.com/x is replaced before its prefix https://a.com.
        var rewrittenText = text;
        foreach (var (original, url) in emitted.OrderByDescending(e => e.Key.Length))
        {
            if (original != url) rewrittenText = rewrittenText.Replace(original, url, StringComparison.Ordinal);
        }

        return new LinkRewriteResult(rewrittenHtml, rewrittenText, links);
    }

    /// <summary>
    /// Appends utm_source/medium/campaign before any fragment. A link that already
    /// carries any utm_* parameter is the author's own tagging and stays as is.
    /// </summary>
    public static string AddUtm(string url, string campaign)
    {
        var hash = url.IndexOf('#');
        var (beforeFragment, fragment) = hash < 0 ? (url, "") : (url[..hash], url[hash..]);
        var query = beforeFragment.IndexOf('?');
        if (query >= 0 && UtmParameterRegex().IsMatch(beforeFragment[(query + 1)..])) return url;

        var separator = query < 0 ? "?" : beforeFragment.EndsWith('?') || beforeFragment.EndsWith('&') ? "" : "&";
        return $"{beforeFragment}{separator}utm_source={UtmSource}&utm_medium={UtmMedium}"
               + $"&utm_campaign={Uri.EscapeDataString(campaign.Trim())}{fragment}";
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Rewriting an unsubscribe link would make opting out depend on our redirect.
    private static bool IsUnsubscribe(string url, string innerHtml) =>
        UnsubscribeRegex().IsMatch(url) || UnsubscribeRegex().IsMatch(WebUtility.HtmlDecode(innerHtml));

    [GeneratedRegex("""<a(?<before>\s[^>]*?\s|\s)href\s*=\s*(?<q>["'])(?<href>.*?)\k<q>(?<after>[^>]*)>(?<inner>.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"(?:^|&)utm_[a-z]+=", RegexOptions.IgnoreCase)]
    private static partial Regex UtmParameterRegex();

    [GeneratedRegex(@"unsubscribe|opt-?out|darse de baja|desuscrib|cancelar (?:la )?suscripci", RegexOptions.IgnoreCase)]
    private static partial Regex UnsubscribeRegex();
}
