namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// A delivery notification already read by the bounce scan, so each one is
/// applied once (spec 14 F4.3). Kept briefly: the scan only looks back days.
/// </summary>
public sealed class ProcessedInboxNotice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid ConnectedEmailAccountId { get; init; }
    public required string ProviderMessageId { get; init; }
    public required DateTimeOffset ProcessedAt { get; init; }
}
