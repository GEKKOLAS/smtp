using System.Net;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Rendering;

/// <summary>
/// Renders {{variables}} (and {{#if}}/{{#each}} sections) with Handlebars.
/// Encoding is context-aware: HTML-body values are HTML-encoded (or sanitized
/// for html-typed vars); subject/preheader values are plain text with control
/// characters stripped to prevent header injection (spec 08 §2.5, 04 §3).
/// </summary>
internal sealed partial class VariableRenderer(Application.Abstractions.Rendering.IHtmlSanitizer sanitizer)
{
    // No text encoder: model values are already correctly encoded/sanitized.
    private readonly IHandlebars _handlebars = Handlebars.Create(
        new HandlebarsConfiguration { TextEncoder = null });

    /// <summary>Model for HTML contexts: text/url encoded, html-typed sanitized.</summary>
    public IReadOnlyDictionary<string, object?> BuildHtmlModel(
        IReadOnlyList<TemplateVariable> schema, IReadOnlyDictionary<string, string?> provided)
    {
        var model = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variable in schema)
        {
            var value = Effective(variable, provided);
            model[variable.Name] = variable.Type switch
            {
                TemplateVariableType.Html => sanitizer.Sanitize(value),
                _ => WebUtility.HtmlEncode(value),
            };
        }
        return model;
    }

    /// <summary>Model for plain-text contexts (subject, preheader, text body).</summary>
    public IReadOnlyDictionary<string, object?> BuildTextModel(
        IReadOnlyList<TemplateVariable> schema, IReadOnlyDictionary<string, string?> provided)
    {
        var model = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variable in schema)
        {
            model[variable.Name] = ControlCharsRegex().Replace(Effective(variable, provided), " ").Trim();
        }
        return model;
    }

    public string Render(string template, IReadOnlyDictionary<string, object?> model)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return _handlebars.Compile(template)(model);
    }

    private static string Effective(TemplateVariable variable, IReadOnlyDictionary<string, string?> provided)
    {
        provided.TryGetValue(variable.Name, out var raw);
        return raw ?? variable.Default ?? string.Empty;
    }

    [GeneratedRegex(@"[\r\n\t\x00-\x1F]")]
    private static partial Regex ControlCharsRegex();
}
