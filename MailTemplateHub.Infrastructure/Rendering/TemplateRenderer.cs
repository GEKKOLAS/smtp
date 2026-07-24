using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// The rendering pipeline (spec 08-rendering.md): compile MJML, sanitize, render
/// variables, inline CSS, resolve assets, and generate plain text. Deterministic.
/// </summary>
internal sealed partial class TemplateRenderer(
    IMjmlCompiler mjmlCompiler,
    IHtmlSanitizer sanitizer,
    IOptions<StorageOptions> storageOptions) : ITemplateRenderer
{
    private const int GmailClipBytes = 102 * 1024;

    public RenderedEmail Render(RenderRequest request)
    {
        var content = request.Content;
        var warnings = new List<RenderWarning>();
        var renderer = new VariableRenderer(sanitizer);

        // 1. Source HTML: compile MJML when it is the source of truth (vars intact).
        var html = ResolveSourceHtml(content);

        // 2. Validate variables and build per-context models.
        ValidateVariables(content, request, warnings);
        var htmlModel = renderer.BuildHtmlModel(content.Variables, request.Variables);
        var textModel = renderer.BuildTextModel(content.Variables, request.Variables);

        // 3. Substitute variables (values in URL positions must be real URLs before
        //    sanitizing, so this runs before the sanitizer).
        var subject = renderer.Render(content.Subject, textModel);
        var preheader = content.Preheader is null ? null : renderer.Render(content.Preheader, textModel);
        html = renderer.Render(html, htmlModel);

        // 4. Sanitize the substituted HTML (security-critical, every render).
        html = sanitizer.Sanitize(html);

        // 5. Inline CSS.
        html = CssInliner.Inline(html);

        // 6. Resolve asset markers (mth-asset://{id} -> URL).
        html = ResolveAssets(html, request.AssetUrls, warnings);

        // 6b. Rewrite baked-in public-asset URLs to the current public base. Assets
        //     are embedded as absolute URLs at author time, so a template built when
        //     the public base was http://localhost:9000/... keeps that host forever —
        //     and it can't resolve in a real inbox. Swapping the origin here (rather
        //     than mutating stored templates) also survives the public base changing,
        //     e.g. an ephemeral dev tunnel URL.
        html = RewritePublicAssetUrls(html);

        // 7. Plain text: render the supplied text, or derive it from the final HTML.
        var text = content.TextBody is { } supplied
            ? renderer.Render(supplied, textModel)
            : PlainTextGenerator.Generate(html);

        // 8. Non-blocking warnings.
        AddSizeWarning(html, warnings);
        AddImageWarnings(html, warnings);
        AddPlaceholderImageWarnings(html, storageOptions.Value.PublicBaseUrl, warnings);

        return new RenderedEmail(subject, preheader, html, text, warnings);
    }

    private string ResolveSourceHtml(TemplateContent content)
    {
        if (content.EditorKind is EditorKind.Html || string.IsNullOrWhiteSpace(content.MjmlSource))
        {
            return content.HtmlBody;
        }

        var result = mjmlCompiler.Compile(content.MjmlSource);
        if (!result.Success)
        {
            throw new MjmlInvalidException(result.Errors);
        }
        return result.Html;
    }

    private static void ValidateVariables(TemplateContent content, RenderRequest request, List<RenderWarning> warnings)
    {
        var missing = new List<string>();
        foreach (var variable in content.Variables)
        {
            request.Variables.TryGetValue(variable.Name, out var provided);
            var hasValue = !string.IsNullOrEmpty(provided) || !string.IsNullOrEmpty(variable.Default);

            if (variable.Required && !hasValue)
            {
                if (request.Strict) { missing.Add(variable.Name); continue; }
                warnings.Add(new RenderWarning("variable.unfilled", $"Variable '{variable.Name}' has no value."));
            }

            // URL-typed values must be absolute http(s).
            var effective = provided ?? variable.Default;
            if (variable.Type == TemplateVariableType.Url && !string.IsNullOrEmpty(effective)
                && !IsAbsoluteHttpUrl(effective))
            {
                if (request.Strict) missing.Add(variable.Name);
                else warnings.Add(new RenderWarning("variable.invalid_url", $"Variable '{variable.Name}' is not a valid URL."));
            }
        }

        if (request.Strict && missing.Count > 0)
        {
            throw new MissingVariablesException(missing);
        }
    }

    private string RewritePublicAssetUrls(string html)
    {
        var opts = storageOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.PublicBaseUrl) || string.IsNullOrWhiteSpace(opts.PublicBucket))
        {
            return html;
        }

        // Matches any absolute http(s) URL pointing at the public bucket, e.g.
        // http://localhost:9000/mth-public/assets/... and rewrites the origin (up to
        // and including the bucket segment) to the currently-configured public base.
        // Handles src, srcset, href, and CSS url(...) uniformly since it operates on
        // the whole rendered string.
        var pattern = $@"https?://[^/\s""'()]+/{Regex.Escape(opts.PublicBucket)}/";
        var replacement = (opts.PublicBaseUrl.TrimEnd('/') + "/").Replace("$", "$$");
        return Regex.Replace(html, pattern, replacement);
    }

    private static string ResolveAssets(string html, IReadOnlyDictionary<Guid, string> assetUrls, List<RenderWarning> warnings)
    {
        return AssetMarkerRegex().Replace(html, match =>
        {
            if (Guid.TryParse(match.Groups[1].Value, out var id) && assetUrls.TryGetValue(id, out var url))
            {
                return url;
            }
            warnings.Add(new RenderWarning("asset.unresolved", "An embedded asset could not be resolved."));
            return match.Value;
        });
    }

    private static void AddSizeWarning(string html, List<RenderWarning> warnings)
    {
        var bytes = System.Text.Encoding.UTF8.GetByteCount(html);
        if (bytes > GmailClipBytes)
        {
            warnings.Add(new RenderWarning("html.too_large",
                $"The HTML is {bytes / 1024} KB; Gmail clips messages over ~102 KB."));
        }
    }

    private static void AddImageWarnings(string html, List<RenderWarning> warnings)
    {
        foreach (Match img in ImgTagRegex().Matches(html))
        {
            if (!AltAttributeRegex().IsMatch(img.Value))
            {
                warnings.Add(new RenderWarning("image.missing_alt", "An image is missing alt text."));
                break;
            }
        }
    }

    // Catches images pointing at fabricated/placeholder addresses (a recurring AI
    // slip: e.g. https://YOUR-DOMAIN.com/email-assets/header.jpg). These resolve to
    // nothing in a real inbox, so the recipient sees a broken image. Surfaced as a
    // warning (with the offending URL) so it is caught in preview before sending,
    // rather than silently shipping broken. Reported once per distinct bad URL.
    private static void AddPlaceholderImageWarnings(string html, string publicBaseUrl, List<RenderWarning> warnings)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in ImageUrlRegex().Matches(html))
        {
            var url = m.Groups[1].Value;
            // Skip the app's own configured public base (a legit asset host that may
            // itself look example-ish, e.g. cdn.example.com) — only foreign
            // placeholder/example addresses are the problem.
            if (!string.IsNullOrWhiteSpace(publicBaseUrl)
                && url.StartsWith(publicBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (PlaceholderHostRegex().IsMatch(url) && seen.Add(url))
            {
                warnings.Add(new RenderWarning("image.placeholder",
                    $"The image \"{url}\" uses a placeholder/example address and won't load in inboxes. "
                    + "Upload the real image under Assets and insert it from your library."));
            }
        }
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [GeneratedRegex(@"mth-asset://([0-9a-fA-F-]{36})")]
    private static partial Regex AssetMarkerRegex();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTagRegex();

    [GeneratedRegex(@"\balt\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex AltAttributeRegex();

    // Pulls the URL out of src="…", background="…", and background-url="…" attributes.
    [GeneratedRegex("""(?:src|background|background-url)\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ImageUrlRegex();

    // Well-known placeholder/example hosts and paths that never resolve for real recipients.
    [GeneratedRegex(@"your-?domain|example\.(?:com|org|net)|your-?cdn|placehold|via\.placeholder|dummyimage|lorempixel|/email-assets/|yourcompany|yourbrand", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderHostRegex();
}
