namespace MailTemplateHub.Application.Common;

/// <summary>
/// AI image generation (OpenAI gpt-image-1). Separate from <see cref="AiOptions"/>
/// because it's a different provider/key. When the key is empty the feature is
/// disabled and the UI hides it.
/// </summary>
public sealed class ImageGenerationOptions
{
    public const string SectionName = "ImageGeneration";

    /// <summary>OpenAI API key. When empty, image generation is disabled.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gpt-image-1";
    public string ApiUrl { get; init; } = "https://api.openai.com/v1/images/generations";

    /// <summary>Default size when the caller doesn't specify one.</summary>
    public string Size { get; init; } = "1024x1024";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
