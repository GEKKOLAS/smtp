using MailTemplateHub.Domain.Common;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// Named, user-owned design container. Holds metadata and a pointer to the current
/// immutable version (spec 05-database.md). Deleting is soft so historical sends
/// still resolve the version they used.
/// </summary>
public sealed class EmailTemplate : IHasTimestamps, ISoftDeletable
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public EmailTemplateVersion? CurrentVersion { get; set; }
    public List<EmailTemplateVersion> Versions { get; init; } = [];
}
