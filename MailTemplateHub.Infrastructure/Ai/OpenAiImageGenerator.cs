using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Ai;

/// <summary>
/// Generates images with OpenAI's Images API (gpt-image-1). The model always
/// returns base64-encoded PNG bytes, which are decoded and handed back for the
/// caller to store as an asset.
/// </summary>
internal sealed class OpenAiImageGenerator(HttpClient httpClient, IOptions<ImageGenerationOptions> options)
    : IAiImageGenerator
{
    private static readonly HashSet<string> AllowedSizes =
        new(StringComparer.OrdinalIgnoreCase) { "1024x1024", "1536x1024", "1024x1536", "auto" };

    private readonly ImageGenerationOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<GeneratedImage> GenerateAsync(string prompt, string? size, CancellationToken ct)
    {
        var chosenSize = size is not null && AllowedSizes.Contains(size) ? size : _options.Size;

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                prompt,
                size = chosenSize,
                n = 1,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AiGenerationException($"The image service was unreachable: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiGenerationException($"The image service returned {(int)response.StatusCode}: {body}");
        }

        return new GeneratedImage(ExtractPng(body), "image/png");
    }

    private static byte[] ExtractPng(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
                && data.GetArrayLength() > 0
                && data[0].TryGetProperty("b64_json", out var b64)
                && b64.GetString() is { Length: > 0 } encoded)
            {
                return Convert.FromBase64String(encoded);
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            // fall through to the typed error below
        }
        throw new AiGenerationException("The image service returned an unexpected response.");
    }
}

/// <summary>Used when no OpenAI key is configured — the feature is off.</summary>
internal sealed class DisabledImageGenerator : IAiImageGenerator
{
    public bool IsConfigured => false;

    public Task<GeneratedImage> GenerateAsync(string prompt, string? size, CancellationToken ct) =>
        throw new AiGenerationException("Image generation isn't configured — add an OpenAI API key.");
}
