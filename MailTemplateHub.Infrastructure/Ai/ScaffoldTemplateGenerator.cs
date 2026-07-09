using System.Text;
using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions.Ai;

namespace MailTemplateHub.Infrastructure.Ai;

/// <summary>
/// Deterministic fallback used when no AI key is configured. It is not a language
/// model — it assembles a clean, valid MJML scaffold from the prompt so the
/// generate → preview → save flow works without external calls.
/// </summary>
internal sealed partial class ScaffoldTemplateGenerator : IAiTemplateGenerator
{
    public bool IsRealAi => false;

    public Task<AiGeneratedTemplate> GenerateAsync(AiTemplateRequest request, CancellationToken ct)
    {
        var color = Sanitize(request.BrandColor) ?? "#2563eb";
        var headline = Headline(request.Prompt);
        var body = Body(request.Prompt);
        var heroImage = request.AssetUrls.FirstOrDefault();

        var mjml = new StringBuilder();
        mjml.AppendLine("<mjml><mj-body background-color=\"#f4f4f5\">");
        mjml.AppendLine("  <mj-section background-color=\"#ffffff\" padding=\"24px 24px 0\">");
        mjml.AppendLine("    <mj-column>");
        if (heroImage is not null)
        {
            mjml.AppendLine($"      <mj-image src=\"{heroImage}\" alt=\"\" padding=\"0 0 16px\" />");
        }
        mjml.AppendLine($"      <mj-text font-size=\"22px\" font-weight=\"700\" color=\"#111827\">Hi {{{{firstName}}}}, {headline}</mj-text>");
        mjml.AppendLine($"      <mj-text font-size=\"15px\" line-height=\"1.6\" color=\"#374151\">{body}</mj-text>");
        mjml.AppendLine($"      <mj-button background-color=\"{color}\" href=\"{{{{ctaUrl}}}}\" padding=\"16px 0\">{{{{ctaLabel}}}}</mj-button>");
        mjml.AppendLine("    </mj-column>");
        mjml.AppendLine("  </mj-section>");
        mjml.AppendLine("  <mj-section padding=\"16px\">");
        mjml.AppendLine("    <mj-column>");
        mjml.AppendLine("      <mj-text align=\"center\" font-size=\"12px\" color=\"#9ca3af\">Sent with Mail Template Hub</mj-text>");
        mjml.AppendLine("    </mj-column>");
        mjml.AppendLine("  </mj-section>");
        mjml.AppendLine("</mj-body></mjml>");

        var variables = new List<AiVariable>
        {
            new("firstName", "text", "Ada"),
            new("ctaLabel", "text", "Get started"),
            new("ctaUrl", "url", "https://example.com"),
        };

        return Task.FromResult(new AiGeneratedTemplate(Subject(request.Prompt), mjml.ToString(), variables));
    }

    private static string Subject(string prompt) =>
        Truncate(Capitalize(FirstSentence(prompt)), 80);

    private static string Headline(string prompt) =>
        HtmlEncode(Truncate(FirstSentence(prompt).TrimEnd('.'), 60).ToLowerInvariant());

    private static string Body(string prompt) =>
        WhitespaceRegex().Replace(HtmlEncode(prompt.Trim()), " ");

    private static string FirstSentence(string text)
    {
        var trimmed = text.Trim();
        var dot = trimmed.IndexOf('.');
        return dot > 0 ? trimmed[..dot] : trimmed;
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";
    private static string HtmlEncode(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string? Sanitize(string? color) =>
        color is not null && HexColorRegex().IsMatch(color) ? color : null;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}
