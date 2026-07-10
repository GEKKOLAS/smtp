using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Ai;

/// <summary>
/// Generates MJML templates with Anthropic's Messages API. Instructs the model to
/// return strict JSON (subject, mjml, variables) so the output plugs straight into
/// the render pipeline.
/// </summary>
internal sealed class AnthropicTemplateGenerator(HttpClient httpClient, IOptions<AiOptions> options)
    : IAiTemplateGenerator
{
    private readonly AiOptions _options = options.Value;

    public bool IsRealAi => true;

    private const string SystemPrompt =
        """
        You are an award-winning email marketing designer, on the level of the best
        in-house designers at Mailchimp, Stripe, or Linear. You produce responsive,
        email-client-safe templates using MJML (https://mjml.io) that look like a
        finished marketing campaign, not a plain-text notice. Rules:

        - Output ONLY a single JSON object, no markdown, no prose.
        - Shape: {"subject": string, "preheader": string, "mjml": string, "variables": [{"name": string, "type": "text"|"url"|"html", "sample": string}]}
        - The "mjml" must be a complete, valid <mjml>...</mjml> document.
        - Personalize with Handlebars placeholders like {{firstName}}; declare each in "variables".
        - Use ONLY these MJML components — the compiler in use does not support any others:
          mj-section, mj-column, mj-group, mj-text, mj-button, mj-image, mj-divider, mj-spacer,
          mj-table, mj-raw, and inside <mj-head>: mj-style (and mj-title/mj-preview if needed).
          Do NOT use mj-attribute, mj-social, mj-social-element, mj-accordion, mj-carousel,
          mj-navbar, or mj-hero — none of these are supported and will fail to compile. For a
          social/brand strip, build it manually as small mj-image icons (with href) side by side
          in an mj-section, not mj-social.

        VISUAL DESIGN — this is the part that matters most:
        - Choose a real, intentional color palette (not just the single brand color echoed back):
          a base/background tone, a surface tone, a text-ink tone with real contrast, and one or
          two accent tones derived from the brand color (a darker shade for a hero band, a lighter
          tint for soft backgrounds). Never default to plain white-on-white.
        - Build at least one hero section with visual weight: use `background-color` on
          `mj-section`, and when a background image is available use the section's
          `background-url` + `background-size="cover"` + `background-repeat="no-repeat"`
          attribute (MJML compiles this to an Outlook-safe VML fallback automatically — this is
          the only correct way to do a background image in MJML, never a raw CSS `background`
          shorthand on mj-text/mj-image). Prefer photographic or illustrative asset URLs the
          caller provided for hero/background placement over plain inline `mj-image`.
        - For a gradient section background (e.g. a gradient header band), `mj-section`'s
          `background-color` attribute only accepts a plain color — it does NOT accept
          `linear-gradient(...)`. To use a gradient, add a `<mj-head><mj-style>.gradient-hero
          { background: linear-gradient(135deg, #COLOR1, #COLOR2) !important; }</mj-style></mj-head>`
          block and put `css-class="gradient-hero"` on that `mj-section`. Keep gradients subtle
          (2-color, similar hues) so text stays legible on top.
        - Establish strong visual hierarchy: one large, confident headline (28-36px, bold), a
          supporting subhead, then body copy at a comfortable reading size (15-16px, 1.6 line
          height). Use spacing (mj-spacer, generous section padding) to create rhythm — don't
          cram everything together.
        - The CTA button must look designed, not default: solid or gradient background using an
          accent color, generous padding (14-18px vertical, 28-36px horizontal), rounded corners
          (border-radius 6-999px depending on tone), and a short, confident label (2-4 words).
        - If multiple images are provided, use them purposefully (hero banner, supporting product
          shots, a footer social/brand strip) rather than dumping them all in one place.
        - Respect accessibility: keep text-to-background contrast readable, never put small body
          text directly over a busy photo without a solid or semi-opaque color band behind it.

        VIDEO CONTENT — email clients do NOT execute JavaScript and do NOT render <video> tags.
        If a video URL and/or video thumbnail URL is supplied in the prompt, NEVER embed a real
        video element. Instead build a "video card": an `mj-section` (or `mj-image`) using the
        supplied thumbnail as the background/image, with a centered play-button affordance placed
        on top using a small circular `mj-button` or bold "▶" `mj-text` styled as a round badge,
        and wrap the whole section/button in a link (`href`) to the video URL so clicking anywhere
        takes the reader to the actual video off-platform. Label it clearly, e.g. "▶ Watch the video".

        - Never invent external image/video URLs — only use ones explicitly given to you.
        - Keep everything single-column and mobile-friendly; test that nothing depends on
          absolute positioning or JS.
        """;

    public async Task<AiGeneratedTemplate> GenerateAsync(AiTemplateRequest request, CancellationToken ct)
    {
        var userPrompt = BuildUserPrompt(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                max_tokens = _options.MaxTokens,
                system = SystemPrompt,
                messages = new[] { new { role = "user", content = userPrompt } },
            }),
        };
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AiGenerationException($"The AI service was unreachable: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiGenerationException($"The AI service returned {(int)response.StatusCode}.");
        }

        var text = ExtractText(body);
        return ParseGenerated(text);
    }

    private static string BuildUserPrompt(AiTemplateRequest request)
    {
        var lines = new List<string> { $"Create a beautiful, attention-grabbing marketing email for: {request.Prompt}" };
        if (!string.IsNullOrWhiteSpace(request.BrandColor))
            lines.Add($"Brand color (build a full palette around this, don't just echo it back): {request.BrandColor}");
        if (!string.IsNullOrWhiteSpace(request.Tone)) lines.Add($"Tone: {request.Tone}");
        if (request.AssetUrls.Count > 0)
            lines.Add("Image URLs available for hero/background/inline use: " + string.Join(", ", request.AssetUrls));
        if (!string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            lines.Add($"A video is linked from this email: {request.VideoUrl}");
            lines.Add(request.VideoThumbnailUrl is not null
                ? $"Use this exact image as the video thumbnail/background (never fabricate one): {request.VideoThumbnailUrl}. Build the video-card pattern described in your instructions, linking the whole card to the video URL above."
                : "No thumbnail image is available for this video — build a clearly-labeled \"▶ Watch the video\" button/card linking to the video URL above instead of a fake thumbnail.");
        }
        if (request.DesiredVariables.Count > 0)
            lines.Add("Include these variables: " + string.Join(", ", request.DesiredVariables));
        return string.Join('\n', lines);
    }

    private static string ExtractText(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array)
            {
                // The first block may be a thinking/tool block; take the first text block.
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out var text)
                        && text.ValueKind == JsonValueKind.String
                        && text.GetString() is { Length: > 0 } value)
                    {
                        return value;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // fall through to the typed error below
        }
        throw new AiGenerationException("The AI service returned an unexpected response.");
    }

    private static AiGeneratedTemplate ParseGenerated(string text)
    {
        var json = ExtractJson(text);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var subject = root.GetProperty("subject").GetString() ?? "Your email";
            var preheader = root.TryGetProperty("preheader", out var ph) ? ph.GetString() : null;
            var mjml = root.GetProperty("mjml").GetString()
                ?? throw new AiGenerationException("The AI response was missing MJML.");

            var variables = new List<AiVariable>();
            if (root.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vars.EnumerateArray())
                {
                    variables.Add(new AiVariable(
                        v.GetProperty("name").GetString() ?? "value",
                        v.TryGetProperty("type", out var t) ? t.GetString() ?? "text" : "text",
                        v.TryGetProperty("sample", out var s) ? s.GetString() ?? "" : ""));
                }
            }
            return new AiGeneratedTemplate(subject, mjml, variables, preheader);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            throw new AiGenerationException("The AI response could not be parsed.");
        }
    }

    // The model is asked for pure JSON, but strip any accidental code fences.
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
