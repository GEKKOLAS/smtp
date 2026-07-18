using System.Text;
using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions.Ai;

namespace MailTemplateHub.Infrastructure.Ai;

/// <summary>
/// Deterministic fallback used when no AI key is configured. It is not a language
/// model — it assembles a clean, design-conscious MJML scaffold (hero band, derived
/// accent palette, styled CTA, optional video card) from the prompt so the
/// generate → preview → save flow works without external calls.
/// </summary>
internal sealed partial class ScaffoldTemplateGenerator : IAiTemplateGenerator
{
    public bool IsRealAi => false;

    public Task<AiGeneratedTemplate> GenerateAsync(AiTemplateRequest request, CancellationToken ct)
    {
        // This deterministic fallback can't meaningfully "edit" arbitrary HTML per an
        // instruction (there's no model behind it) — echo the existing document back
        // unchanged rather than replacing it with an unrelated MJML scaffold.
        if (request.CurrentHtml is not null)
        {
            return Task.FromResult(new AiGeneratedTemplate(
                Subject(request.Prompt), request.CurrentHtml, [], Preheader(request.Prompt)));
        }

        var accent = Sanitize(request.BrandColor) ?? "#2563eb";
        var hero = Darken(accent, 0.45);
        var tint = Lighten(accent, 0.92);
        var heroImage = request.BackgroundImageUrl ?? request.AssetUrls.FirstOrDefault();
        var headline = Headline(request.Prompt);
        var body = Body(request.Prompt);

        var mjml = new StringBuilder();
        mjml.AppendLine("<mjml><mj-body background-color=\"#f4f4f5\">");

        // Hero band: a photographic background when an asset is available, otherwise
        // a solid dark-accent band — either way it reads as a designed header, not a
        // plain white page.
        if (heroImage is not null)
        {
            mjml.AppendLine($"  <mj-section background-url=\"{heroImage}\" background-size=\"cover\" background-repeat=\"no-repeat\" padding=\"56px 24px\">");
        }
        else
        {
            mjml.AppendLine($"  <mj-section background-color=\"{hero}\" padding=\"56px 24px\">");
        }
        mjml.AppendLine("    <mj-column>");
        mjml.AppendLine($"      <mj-text align=\"center\" font-size=\"30px\" font-weight=\"800\" line-height=\"1.25\" color=\"#ffffff\">Hi {{{{firstName}}}}, {headline}</mj-text>");
        mjml.AppendLine("    </mj-column>");
        mjml.AppendLine("  </mj-section>");

        // Body card.
        mjml.AppendLine("  <mj-section background-color=\"#ffffff\" padding=\"32px 24px\">");
        mjml.AppendLine("    <mj-column>");
        mjml.AppendLine($"      <mj-text font-size=\"15px\" line-height=\"1.6\" color=\"#374151\">{body}</mj-text>");
        mjml.AppendLine("      <mj-spacer height=\"8px\" />");
        mjml.AppendLine($"      <mj-button background-color=\"{accent}\" border-radius=\"999px\" font-weight=\"700\" href=\"{{{{ctaUrl}}}}\" padding=\"24px 0 8px\" inner-padding=\"16px 34px\">{{{{ctaLabel}}}}</mj-button>");
        mjml.AppendLine("    </mj-column>");
        mjml.AppendLine("  </mj-section>");

        AppendVideoCardIfPresent(mjml, request, hero);

        mjml.AppendLine($"  <mj-section background-color=\"{tint}\" padding=\"16px\">");
        mjml.AppendLine("    <mj-column>");
        mjml.AppendLine("      <mj-text align=\"center\" font-size=\"12px\" color=\"#6b7280\">Sent with Mail Template Hub</mj-text>");
        mjml.AppendLine("    </mj-column>");
        mjml.AppendLine("  </mj-section>");
        mjml.AppendLine("</mj-body></mjml>");

        var variables = new List<AiVariable>
        {
            new("firstName", "text", "Ada"),
            new("ctaLabel", "text", "Get started"),
            new("ctaUrl", "url", "https://example.com"),
        };

        return Task.FromResult(new AiGeneratedTemplate(
            Subject(request.Prompt), mjml.ToString(), variables, Preheader(request.Prompt)));
    }

    private static void AppendVideoCardIfPresent(StringBuilder mjml, AiTemplateRequest request, string fallbackBg)
    {
        if (string.IsNullOrWhiteSpace(request.VideoUrl)) return;

        // Email clients don't render <video> — link out to a static thumbnail with a
        // play-button affordance instead of embedding anything playable.
        if (request.VideoThumbnailUrl is { } thumb)
        {
            mjml.AppendLine($"  <mj-section background-url=\"{thumb}\" background-size=\"cover\" background-repeat=\"no-repeat\" padding=\"48px 24px\">");
            mjml.AppendLine("    <mj-column>");
            mjml.AppendLine($"      <mj-button background-color=\"#111827cc\" border-radius=\"999px\" font-weight=\"700\" href=\"{request.VideoUrl}\" padding=\"12px 0\" inner-padding=\"14px 28px\">▶ Watch the video</mj-button>");
            mjml.AppendLine("    </mj-column>");
            mjml.AppendLine("  </mj-section>");
        }
        else
        {
            mjml.AppendLine($"  <mj-section background-color=\"{fallbackBg}\" padding=\"32px 24px\">");
            mjml.AppendLine("    <mj-column>");
            mjml.AppendLine($"      <mj-button background-color=\"#ffffff\" color=\"{fallbackBg}\" border-radius=\"999px\" font-weight=\"700\" href=\"{request.VideoUrl}\" padding=\"12px 0\" inner-padding=\"14px 28px\">▶ Watch the video</mj-button>");
            mjml.AppendLine("    </mj-column>");
            mjml.AppendLine("  </mj-section>");
        }
    }

    private static string Subject(string prompt) =>
        Truncate(Capitalize(FirstSentence(prompt)), 80);

    private static string Preheader(string prompt)
    {
        var rest = prompt.Trim();
        var dot = rest.IndexOf('.');
        var tail = dot >= 0 && dot + 1 < rest.Length ? rest[(dot + 1)..].Trim() : rest;
        return Truncate(HtmlEncode(WhitespaceRegex().Replace(tail, " ")), 90);
    }

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
        color is not null && HexColorRegex().IsMatch(color) ? Expand(color) : null;

    // Expands #abc to #aabbcc so the RGB math below always sees 6 hex digits.
    private static string Expand(string hex) =>
        hex.Length == 4
            ? $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}"
            : hex;

    /// <summary>Blends the color toward black by <paramref name="amount"/> (0-1) for a hero/accent band.</summary>
    private static string Darken(string hex, double amount) => Blend(hex, 0, 0, 0, amount);

    /// <summary>Blends the color toward white by <paramref name="amount"/> (0-1) for a soft tint.</summary>
    private static string Lighten(string hex, double amount) => Blend(hex, 255, 255, 255, amount);

    private static string Blend(string hex, int r2, int g2, int b2, double amount)
    {
        var (r, g, b) = (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
        var mixed = (
            Mix(r, r2, amount),
            Mix(g, g2, amount),
            Mix(b, b2, amount));
        return $"#{mixed.Item1:x2}{mixed.Item2:x2}{mixed.Item3:x2}";
    }

    private static int Mix(int a, int b, double amount) => (int)Math.Round(a + (b - a) * amount);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}
