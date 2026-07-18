using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Ai;

/// <summary>
/// Generates MJML templates with Anthropic's Messages API. Instructs the model to
/// return strict JSON (subject, mjml, variables) so the output plugs straight into
/// the render pipeline.
/// </summary>
internal sealed class AnthropicTemplateGenerator(
    HttpClient httpClient, IOptions<AiOptions> options, ILogger<AnthropicTemplateGenerator> logger)
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
        - The "mjml" field holds the full generated markup. By default (and whenever an "EXISTING
          MJML" block is given, or none at all) that markup is MJML: a complete, valid
          <mjml>...</mjml> document. If instead the message contains an "EXISTING HTML" block,
          this template does NOT use MJML — put a complete, valid HTML5 email document
          (<html>...<body>...</body></html>) in that same "mjml" field instead, using ONLY
          standard HTML5 tags and attributes — NEVER emit any mj-* tag (mj-section, mj-spacer,
          etc.) or any other MJML syntax anywhere in HTML output, not even as a leftover or
          placeholder. Use simple, inline-friendly table-or-div layout (a CSS inliner runs on your
          output afterward, so a <style> block or inline styles both work). Everything else in
          this prompt (visual design, video cards, image roles) applies equally to both output
          modes.
        - Personalize RECIPIENT-SPECIFIC content with Handlebars placeholders like {{firstName}}
          or {{ctaUrl}}; declare each one in "variables". Image/video/logo/background URLs are a
          completely different thing — every URL given to you below (background image, header
          logo, footer logo, other image URLs, video thumbnail) is already final and resolved:
          embed each one LITERALLY as a plain string in the markup (src="...", url(...), href="...")
          exactly where instructed. Never wrap a given image/video URL in {{...}} syntax and never
          list one as a "variables" entry — only true per-recipient fields belong there.
        - Use ONLY these MJML components when producing MJML — the compiler in use does not support any others:
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
        - If a background image, header logo, and/or footer logo are explicitly labeled below,
          use each in exactly that role and nowhere else: the background image behind a
          hero/section (never as a small inline picture), the header logo small at the very top
          of the email, the footer logo small and muted in the footer/signature area. Never
          repurpose one labeled role for another, and never fabricate a logo or background image
          that wasn't given to you.
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

        EDITING AN EXISTING TEMPLATE — if the user message includes an "EXISTING MJML" or
        "EXISTING HTML" block, you are revising that template, not starting from scratch. Treat
        the rest of the message as the edit instruction. Keep everything about the existing design
        (copy, structure, palette, images, variables) that the instruction doesn't ask you to
        change, and apply the requested change precisely. Always return the FULL, complete
        revised document in the same markup language it was given to you in (and the full JSON
        object) — never a diff or a partial fragment, and never switch MJML and HTML.
        """;

    // This machine's network path is flaky against api.anthropic.com in more than
    // one way — verified live: a streamed response that had already received
    // headers and content deltas still got aborted at ~30.0s (not an idle
    // timeout, since it was actively receiving bytes), and separately a plain DNS
    // lookup ("No such host is known") failed outright for one attempt while the
    // very next one worked fine. Both are transient, so every attempt is retried
    // rather than failing the whole generation on the first hiccup. When a
    // mid-stream drop leaves partial text, the retry is a continuation: Anthropic
    // supports prefilling the next request's final "assistant" message with the
    // text already received, so the model picks up exactly where it left off
    // instead of starting over. Each retry is a fresh connection/DNS lookup, so
    // this works through both failure modes as many times as it takes.
    private const int MaxContinuationAttempts = 8;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task<AiGeneratedTemplate> GenerateAsync(AiTemplateRequest request, CancellationToken ct)
    {
        var userPrompt = BuildUserPrompt(request);
        var model = request.UseAdvancedModel && !string.IsNullOrWhiteSpace(_options.AdvancedModel)
            ? _options.AdvancedModel
            : _options.Model;

        var accumulated = new StringBuilder();
        var completed = false;

        for (var attempt = 0; attempt < MaxContinuationAttempts && !completed; attempt++)
        {
            if (attempt > 0) await Task.Delay(RetryDelay, ct);

            // Assistant-message prefill (ending the conversation on an assistant
            // turn for the model to continue inline) is rejected outright by some
            // models ("This model does not support assistant message prefill") —
            // verified live against claude-opus-4-8. Use an ordinary multi-turn
            // "continue" exchange instead: it always ends on a user message, so
            // it works regardless of prefill support.
            object[] messages = accumulated.Length == 0
                ? [new { role = "user", content = userPrompt }]
                : [
                    new { role = "user", content = userPrompt },
                    new { role = "assistant", content = accumulated.ToString() },
                    new
                    {
                        role = "user",
                        content = "Continue exactly where you left off above. Do not repeat any text you " +
                            "already wrote, do not restart the JSON object, and do not add any markdown, " +
                            "prose, or commentary — output only the raw continuation text so it can be " +
                            "concatenated directly onto what you already wrote.",
                    },
                ];

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
            {
                Content = JsonContent.Create(new
                {
                    model,
                    max_tokens = _options.MaxTokens,
                    system = SystemPrompt,
                    messages,
                    stream = true,
                }),
            };
            httpRequest.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
            httpRequest.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException) when (attempt < MaxContinuationAttempts - 1)
            {
                // Transient failure to even connect (DNS hiccup, refused connection) —
                // worth a fresh attempt rather than failing the whole generation.
                continue;
            }
            catch (HttpRequestException ex)
            {
                throw new AiGenerationException($"The AI service was unreachable: {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new AiGenerationException($"The AI service returned {(int)response.StatusCode}: {errorBody}");
            }

            var (chunk, reachedStop) = await ReadStreamedTextAsync(response, ct);
            accumulated.Append(chunk);
            completed = reachedStop;
            logger.LogInformation(
                "AI generation attempt {Attempt}: received {ChunkLength} chars this round, {TotalLength} total, completed={Completed}",
                attempt, chunk.Length, accumulated.Length, completed);
        }

        if (accumulated.Length == 0)
        {
            throw new AiGenerationException("The AI service returned an unexpected response.");
        }
        try
        {
            return ParseGenerated(accumulated.ToString());
        }
        catch (AiGenerationException)
        {
            var full = accumulated.ToString();
            logger.LogWarning(
                "AI response could not be parsed. Length={Length}. Start={Start} End={End}",
                full.Length,
                full[..Math.Min(300, full.Length)],
                full[Math.Max(0, full.Length - 300)..]);
            throw;
        }
    }

    private static async Task<(string Text, bool Completed)> ReadStreamedTextAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        var text = new StringBuilder();
        string? stopReason = null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                using var doc = JsonDocument.Parse(line["data: ".Length..]);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                if (type == "error")
                {
                    var message = root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "unknown error";
                    throw new AiGenerationException($"The AI service reported an error: {message}");
                }

                // message_delta carries the stop_reason just before message_stop.
                // "max_tokens" means the response was cut off mid-document, not
                // finished — that must be treated the same as a dropped connection
                // (retry as a continuation), or the truncated JSON/MJML never parses.
                if (type == "message_delta"
                    && root.TryGetProperty("delta", out var msgDelta)
                    && msgDelta.TryGetProperty("stop_reason", out var sr))
                {
                    stopReason = sr.GetString();
                }

                if (type == "message_stop")
                {
                    return (text.ToString(), stopReason is null or "end_turn" or "stop_sequence");
                }

                if (type == "content_block_delta"
                    && root.TryGetProperty("delta", out var delta)
                    && delta.TryGetProperty("type", out var dt) && dt.GetString() == "text_delta"
                    && delta.TryGetProperty("text", out var dText))
                {
                    text.Append(dText.GetString());
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is IOException or OperationCanceledException)
        {
            // The connection was dropped mid-stream by the network path, not by our
            // own caller — surfaces as IOException or a wrapping TaskCanceledException
            // depending on exactly where the reset lands. Return what was received so
            // GenerateAsync can retry as a continuation.
        }

        return (text.ToString(), false);
    }

    private static string BuildUserPrompt(AiTemplateRequest request)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.CurrentHtml))
        {
            lines.Add("EXISTING HTML (the current template — revise it, don't start over; this template does not use MJML):");
            lines.Add($"```\n{request.CurrentHtml}\n```");
            lines.Add($"Edit instruction: {request.Prompt}");
        }
        else if (!string.IsNullOrWhiteSpace(request.CurrentMjml))
        {
            lines.Add("EXISTING MJML (the current template — revise it, don't start over):");
            lines.Add($"```\n{request.CurrentMjml}\n```");
            lines.Add($"Edit instruction: {request.Prompt}");
        }
        else
        {
            lines.Add($"Create a beautiful, attention-grabbing marketing email for: {request.Prompt}");
        }
        if (!string.IsNullOrWhiteSpace(request.BrandColor))
            lines.Add($"Brand color (build a full palette around this, don't just echo it back): {request.BrandColor}");
        if (!string.IsNullOrWhiteSpace(request.Tone)) lines.Add($"Tone: {request.Tone}");
        if (!string.IsNullOrWhiteSpace(request.BackgroundImageUrl))
            lines.Add($"Background image (use in the hero/section-background role only): {request.BackgroundImageUrl}");
        if (!string.IsNullOrWhiteSpace(request.HeaderLogoUrl))
            lines.Add($"Header logo (small, at the very top of the email only): {request.HeaderLogoUrl}");
        if (!string.IsNullOrWhiteSpace(request.FooterLogoUrl))
            lines.Add($"Footer logo (small, in the footer/signature area only): {request.FooterLogoUrl}");
        if (request.AssetUrls.Count > 0)
            lines.Add("Other image URLs available for supporting/inline use: " + string.Join(", ", request.AssetUrls));
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
