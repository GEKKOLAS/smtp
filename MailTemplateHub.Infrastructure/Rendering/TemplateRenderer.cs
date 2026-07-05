using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// The rendering pipeline (spec 08-rendering.md): compile MJML, sanitize, render
/// variables, inline CSS, resolve assets, and generate plain text. Deterministic.
/// </summary>
internal sealed partial class TemplateRenderer(
    IMjmlCompiler mjmlCompiler,
    IHtmlSanitizer sanitizer) : ITemplateRenderer
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

        // 7. Plain text: render the supplied text, or derive it from the final HTML.
        var text = content.TextBody is { } supplied
            ? renderer.Render(supplied, textModel)
            : PlainTextGenerator.Generate(html);

        // 8. Non-blocking warnings.
        AddSizeWarning(html, warnings);
        AddImageWarnings(html, warnings);

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

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [GeneratedRegex(@"mth-asset://([0-9a-fA-F-]{36})")]
    private static partial Regex AssetMarkerRegex();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTagRegex();

    [GeneratedRegex(@"\balt\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex AltAttributeRegex();
}
