namespace MailTemplateHub.Application.Abstractions.Ai;

public sealed record GeneratedImage(byte[] Bytes, string MimeType);

/// <summary>
/// Generates a raster image from a natural-language prompt (OpenAI gpt-image-1).
/// Returns the raw bytes; the caller stores them as an asset. Disabled (no key)
/// implementations report <see cref="IsConfigured"/> = false and throw
/// <see cref="AiGenerationException"/> from <see cref="GenerateAsync"/>.
/// </summary>
public interface IAiImageGenerator
{
    /// <summary>True when an API key is configured; the UI hides the feature when false.</summary>
    bool IsConfigured { get; }

    Task<GeneratedImage> GenerateAsync(string prompt, string? size, CancellationToken ct);
}
