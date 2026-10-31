using MailTemplateHub.Application.Common;

namespace MailTemplateHub.Application.Abstractions.Ai;

public sealed record AiTemplateRequest(
    string Prompt,
    string? BrandColor,
    string? Tone,
    IReadOnlyList<string> AssetUrls,
    IReadOnlyList<string> DesiredVariables,
    string? VideoUrl,
    string? VideoThumbnailUrl,
    bool UseAdvancedModel = false,
    string? CurrentMjml = null,
    // Set instead of CurrentMjml when editing a raw-HTML (non-MJML) template.
    string? CurrentHtml = null,
    string? BackgroundImageUrl = null,
    string? HeaderLogoUrl = null,
    string? FooterLogoUrl = null,
    // A brand kit replaces the single BrandColor guess with exact values (spec 14 F1).
    AiBrandKit? Brand = null);

/// <summary>A brand kit flattened for prompting: literal values to use, not suggestions.</summary>
public sealed record AiBrandKit(
    string Name,
    string Primary,
    string Secondary,
    string Background,
    string Surface,
    string Text,
    string Muted,
    string Link,
    string HeadingFontStack,
    string BodyFontStack,
    string? LogoUrl,
    int ButtonRadius,
    string ButtonStyle,
    int CardRadius,
    string? Voice);

public sealed record AiVariable(string Name, string Type, string Sample);

public sealed record AiGeneratedTemplate(
    string Subject,
    string MjmlSource,
    IReadOnlyList<AiVariable> Variables,
    string? Preheader = null);

/// <summary>
/// Turns a natural-language prompt into an MJML email template. Implemented by a
/// real LLM (Anthropic) or a deterministic scaffold fallback when no key is set.
/// </summary>
public interface IAiTemplateGenerator
{
    /// <summary>True for a real model; false for the scaffold fallback.</summary>
    bool IsRealAi { get; }

    Task<AiGeneratedTemplate> GenerateAsync(AiTemplateRequest request, CancellationToken ct);
}

/// <summary>The AI provider returned output that could not be used (maps to 422).</summary>
public sealed class AiGenerationException(string message) : AppException("ai.generation_failed", message);
