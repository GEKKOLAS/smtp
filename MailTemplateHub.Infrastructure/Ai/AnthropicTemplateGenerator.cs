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
        You are an expert HTML email designer. You produce responsive, email-client-safe
        templates using MJML (https://mjml.io). Rules:
        - Output ONLY a single JSON object, no markdown, no prose.
        - Shape: {"subject": string, "mjml": string, "variables": [{"name": string, "type": "text"|"url"|"html", "sample": string}]}
        - The "mjml" must be a complete, valid <mjml>...</mjml> document.
        - Personalize with Handlebars placeholders like {{firstName}}; declare each in "variables".
        - Use only MJML components (mj-section, mj-column, mj-text, mj-button, mj-image, mj-divider, mj-spacer).
        - If image URLs are provided, use them in <mj-image>. Never invent external URLs.
        - Keep it clean, modern, and single-column-friendly for mobile.
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
        var lines = new List<string> { $"Create an email template for: {request.Prompt}" };
        if (!string.IsNullOrWhiteSpace(request.BrandColor)) lines.Add($"Brand color: {request.BrandColor}");
        if (!string.IsNullOrWhiteSpace(request.Tone)) lines.Add($"Tone: {request.Tone}");
        if (request.AssetUrls.Count > 0) lines.Add("Image URLs to use: " + string.Join(", ", request.AssetUrls));
        if (request.DesiredVariables.Count > 0)
            lines.Add("Include these variables: " + string.Join(", ", request.DesiredVariables));
        return string.Join('\n', lines);
    }

    private static string ExtractText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            return content[0].GetProperty("text").GetString() ?? string.Empty;
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
            return new AiGeneratedTemplate(subject, mjml, variables);
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
