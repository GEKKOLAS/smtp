using MailTemplateHub.Application.Abstractions;

namespace MailTemplateHub.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
