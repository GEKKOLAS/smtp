namespace MailTemplateHub.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
