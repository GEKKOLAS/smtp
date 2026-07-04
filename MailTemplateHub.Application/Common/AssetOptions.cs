namespace MailTemplateHub.Application.Common;

public sealed class AssetOptions
{
    public const string SectionName = "Assets";

    public long MaxImageBytes { get; init; } = 10 * 1024 * 1024; // 10 MB
    public long MaxFileBytes { get; init; } = 25 * 1024 * 1024;  // 25 MB
    public long PerUserQuotaBytes { get; init; } = 1024L * 1024 * 1024; // 1 GB
}
