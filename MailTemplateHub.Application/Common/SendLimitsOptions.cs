namespace MailTemplateHub.Application.Common;

public sealed class SendLimitsOptions
{
    public const string SectionName = "SendLimits";

    public int MaxRecipients { get; init; } = 50;
    public long MaxMessageBytes { get; init; } = 25 * 1024 * 1024; // 25 MB total budget
    public int MaxAttemptsPerRecipient { get; init; } = 5;
    public int MinScheduleMinutes { get; init; } = 2;
    public int MaxScheduleDays { get; init; } = 365;

    /// <summary>Snapshot retention is enforced by the cleanup job; kept here for reference.</summary>
    public int SnapshotRetentionDays { get; init; } = 180;
}
