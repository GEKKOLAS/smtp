using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Features.Templates;

/// <summary>
/// Parses the snake_case usage values used by the API (inline_cid, hosted_image,
/// attachment) into the <see cref="TemplateAssetUsage"/> enum.
/// </summary>
public static class TemplateAssetUsageParser
{
    public static bool TryParse(string value, out TemplateAssetUsage usage)
    {
        // Strip underscores so "hosted_image" matches "HostedImage" case-insensitively.
        var normalized = value?.Replace("_", "", StringComparison.Ordinal) ?? string.Empty;
        return Enum.TryParse(normalized, ignoreCase: true, out usage);
    }
}
