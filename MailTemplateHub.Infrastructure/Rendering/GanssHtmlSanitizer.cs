using Ganss.Xss;

namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// Allowlist HTML sanitizer for email (spec 04 §3): strips scripts, event
/// handlers, iframes/objects/forms, and javascript:/data:text/html URIs, while
/// keeping email-safe structure, inline styles, tables, and images.
/// </summary>
internal sealed class GanssHtmlSanitizer : Application.Abstractions.Rendering.IHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public GanssHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // Structure and email-safe formatting.
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "a", "b", "strong", "i", "em", "u", "s", "span", "div", "p", "br", "hr",
                     "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote", "pre", "code",
                     "table", "thead", "tbody", "tfoot", "tr", "td", "th", "col", "colgroup", "caption",
                     "img", "center", "font", "small", "sub", "sup", "style",
                     // HTML5 structural/semantic tags for custom templates (safe: no scripting).
                     "section", "article", "header", "footer", "main", "nav", "aside",
                     "figure", "figcaption", "picture", "source", "mark", "time", "abbr",
                     "cite", "q", "del", "ins", "wbr", "address", "dl", "dt", "dd",
                 })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[]
                 {
                     "href", "src", "alt", "title", "width", "height", "align", "valign",
                     "border", "cellpadding", "cellspacing", "bgcolor", "color", "style", "class",
                     "role", "target", "colspan", "rowspan", "dir", "background",
                     "srcset", "sizes", "media", "datetime", "id",
                 })
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        // Email needs a broad set of CSS properties for layout.
        _sanitizer.AllowedCssProperties.Clear();
        foreach (var prop in new[]
                 {
                     "color", "background", "background-color", "background-image", "font", "font-family",
                     "font-size", "font-weight", "font-style", "line-height", "letter-spacing",
                     "text-align", "text-decoration", "text-transform", "vertical-align",
                     "width", "height", "min-width", "max-width", "margin", "margin-top", "margin-bottom",
                     "margin-left", "margin-right", "padding", "padding-top", "padding-bottom",
                     "padding-left", "padding-right", "border", "border-top", "border-bottom",
                     "border-left", "border-right", "border-radius", "border-collapse", "border-spacing",
                     "display", "mso-table-lspace", "mso-table-rspace",
                 })
        {
            _sanitizer.AllowedCssProperties.Add(prop);
        }

        // Only web/mail-safe URL schemes; javascript:/data:text-html are dropped.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("cid");        // inline images in the final MIME
        _sanitizer.AllowedSchemes.Add("mth-asset");  // editor asset markers, resolved later

        // data: images are allowed but bounded; everything else data: is stripped.
        _sanitizer.AllowDataAttributes = false;

        // Preserve MSO conditional comments (how Outlook buttons/ghost tables work),
        // but only script-free ones — everything else is removed as usual.
        _sanitizer.RemovingComment += (_, e) =>
        {
            var text = e.Comment.TextContent ?? string.Empty;
            var isMso = text.TrimStart().StartsWith("[if", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("[endif]", StringComparison.OrdinalIgnoreCase);
            if (isMso && !text.Contains("<script", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
            }
        };
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
