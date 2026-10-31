using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Ai;

/// <summary>
/// Rewrites short email copy with Anthropic's Messages API (spec 14 F2). Small,
/// non-streaming calls: the answer is a few short strings in strict JSON.
/// </summary>
internal sealed class AnthropicCopyWriter(
    HttpClient httpClient, IOptions<AiOptions> options, ILogger<AnthropicCopyWriter> logger) : IAiCopyWriter
{
    private const int MaxTokens = 1500;

    private const string SystemPrompt =
        """
        You rewrite short pieces of email copy. Output ONLY a JSON object, no markdown, no prose:
        {"variations": [{"text": string, "note": string}]}

        Rules:
        - Return exactly the number of variations asked for, each clearly different from the
          original and from each other.
        - Keep the language of the original text.
        - Keep every {{placeholder}} exactly as written (same names, same braces). Never drop
          one and never add a new one.
        - Never add facts, numbers, prices, discounts, dates, names, guarantees or claims that
          are not in the original or the context. Change wording, order and emphasis only.
        - No spam patterns: no ALL-CAPS words, no "!!!", no clickbait, no "free" unless the
          original says it. No emojis unless the original has them.
        - "note" is one short sentence, in the original's language, saying what you changed.

        By kind:
        - subject: specific, ideally 60 characters or fewer, no trailing period.
        - preheader: 100 characters or fewer; complements the subject instead of repeating it.
        - cta: 2 to 5 words, starts with a verb.
        - body: same length as the original or shorter; keep its paragraph breaks.

        By tone:
        - promotional: energetic and benefit-led, still honest.
        - professional: clear, courteous, concise.
        - technical: precise and concrete, no hype.
        - followUp: a friendly, low-pressure nudge that refers back to an earlier message.
        """;

    private readonly AiOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<CopyVariation>> RewriteAsync(CopyRequest request, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                max_tokens = MaxTokens,
                system = SystemPrompt,
                messages = new[] { new { role = "user", content = BuildUserPrompt(request) } },
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

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Copy rewrite failed with {Status}: {Body}", (int)response.StatusCode, body);
                throw new AiGenerationException($"The AI service returned {(int)response.StatusCode}.");
            }
            return Parse(body);
        }
    }

    internal static string BuildUserPrompt(CopyRequest request)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Kind: {request.Kind}");
        prompt.AppendLine($"Tone: {request.Tone}");
        prompt.AppendLine($"Number of variations: {request.Count}");
        if (!string.IsNullOrWhiteSpace(request.BrandVoice))
            prompt.AppendLine($"Brand voice to follow: {request.BrandVoice}");
        if (!string.IsNullOrWhiteSpace(request.Context))
            prompt.AppendLine($"Context (facts you may rely on): {request.Context}");
        prompt.AppendLine("Original:");
        prompt.AppendLine("```");
        prompt.AppendLine(request.Text);
        prompt.Append("```");
        return prompt.ToString();
    }

    internal static IReadOnlyList<CopyVariation> Parse(string responseBody)
    {
        try
        {
            using var envelope = JsonDocument.Parse(responseBody);
            // The content array can lead with non-text blocks; use the first text block.
            var text = envelope.RootElement.GetProperty("content").EnumerateArray()
                .Where(block => block.GetProperty("type").GetString() == "text")
                .Select(block => block.GetProperty("text").GetString())
                .FirstOrDefault() ?? string.Empty;

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) throw new AiGenerationException("The AI returned no suggestions.");

            using var json = JsonDocument.Parse(text[start..(end + 1)]);
            return json.RootElement.GetProperty("variations").EnumerateArray()
                .Select(v => new CopyVariation(
                    v.GetProperty("text").GetString() ?? string.Empty,
                    v.TryGetProperty("note", out var note) ? note.GetString() : null))
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiGenerationException("The AI returned suggestions in an unexpected format.");
        }
    }
}

/// <summary>Used when no Anthropic key is configured — the copy assistant is off.</summary>
internal sealed class DisabledCopyWriter : IAiCopyWriter
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<CopyVariation>> RewriteAsync(CopyRequest request, CancellationToken ct) =>
        throw new AiUnavailableException("The copy assistant isn't configured — add an Anthropic API key.");
}
