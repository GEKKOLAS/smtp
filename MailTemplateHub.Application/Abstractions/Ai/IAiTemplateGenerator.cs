using MailTemplateHub.Application.Common;

namespace MailTemplateHub.Application.Abstractions.Ai;

public sealed record AiTemplateRequest(
    string Prompt,
    string? BrandColor,
    string? Tone,
    IReadOnlyList<string> AssetUrls,
    IReadOnlyList<string> DesiredVariables);

public sealed record AiVariable(string Name, string Type, string Sample);

public sealed record AiGeneratedTemplate(
    string Subject,
    string MjmlSource,
    IReadOnlyList<AiVariable> Variables);

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
