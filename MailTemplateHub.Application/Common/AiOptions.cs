namespace MailTemplateHub.Application.Common;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Anthropic API key. When empty, the scaffold fallback generator is used.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "claude-sonnet-5";
    public string ApiUrl { get; init; } = "https://api.anthropic.com/v1/messages";
    public int MaxTokens { get; init; } = 8000;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
