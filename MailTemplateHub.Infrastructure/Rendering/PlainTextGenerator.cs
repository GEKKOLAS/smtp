using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// Derives a readable plain-text alternative from final HTML (spec 08 §2.8):
/// headings and blocks become lines, links expand to "text (url)", images to
/// their alt text.
/// </summary>
internal static class PlainTextGenerator
{
    private static readonly HtmlParser Parser = new();

    public static string Generate(string html)
    {
        var document = Parser.ParseDocument(html);
        var body = document.Body;
        if (body is null) return string.Empty;

        var builder = new StringBuilder();
        Walk(body, builder);

        // Collapse excess blank lines and trailing whitespace.
        var lines = builder.ToString()
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.TrimEnd());
        var text = string.Join('\n', lines);
        while (text.Contains("\n\n\n")) text = text.Replace("\n\n\n", "\n\n");
        return text.Trim();
    }

    private static void Walk(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    var value = System.Text.RegularExpressions.Regex.Replace(text.TextContent, @"\s+", " ");
                    if (value.Length > 0) builder.Append(value);
                    break;

                case IElement element:
                    AppendElement(element, builder);
                    break;
            }
        }
    }

    private static void AppendElement(IElement element, StringBuilder builder)
    {
        switch (element.TagName.ToLowerInvariant())
        {
            case "br":
                builder.Append('\n');
                return;
            case "hr":
                builder.Append("\n----------\n");
                return;
            case "img":
                var alt = element.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(alt)) builder.Append('[').Append(alt).Append(']');
                return;
            case "style" or "head" or "title":
                return;
        }

        var isBlock = element.TagName.ToLowerInvariant() is
            "p" or "div" or "table" or "tr" or "li" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            or "blockquote" or "ul" or "ol" or "header" or "footer" or "section";

        if (isBlock) EnsureNewline(builder);

        if (string.Equals(element.TagName, "a", StringComparison.OrdinalIgnoreCase))
        {
            var href = element.GetAttribute("href");
            Walk(element, builder);
            if (!string.IsNullOrWhiteSpace(href) && !href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                && !href.StartsWith('#'))
            {
                builder.Append(" (").Append(href).Append(')');
            }
            return;
        }

        Walk(element, builder);
        if (isBlock) builder.Append('\n');
    }

    private static void EnsureNewline(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '\n') builder.Append('\n');
    }
}
