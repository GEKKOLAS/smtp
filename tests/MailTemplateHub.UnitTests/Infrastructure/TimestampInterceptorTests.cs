using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Infrastructure.Time;

namespace MailTemplateHub.UnitTests.Infrastructure;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_utc_offset()
    {
        IClock clock = new SystemClock();

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }
}
