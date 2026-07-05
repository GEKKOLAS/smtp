using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

public sealed class EmailSendAttachment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid SendJobId { get; init; }
    public required Guid AssetId { get; init; }
    public required SendAttachmentDisposition Disposition { get; init; }
    public string? ContentId { get; init; }
    public string? FilenameOverride { get; init; }
    public DateTimeOffset CreatedAt { get; set; }

    public Asset? Asset { get; init; }
}
