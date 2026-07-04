using MailTemplateHub.Domain.Common;

namespace MailTemplateHub.Domain.Entities;

public sealed class User : IHasTimestamps, ISoftDeletable
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Email { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public List<UserSession> Sessions { get; init; } = [];
}
