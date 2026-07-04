namespace MailTemplateHub.Domain.Common;

/// <summary>Rows are hidden by a global query filter instead of being removed.</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
