using MailTemplateHub.Domain.Errors;

namespace MailTemplateHub.UnitTests.Domain;

public class DomainExceptionTests
{
    [Fact]
    public void Carries_machine_readable_code()
    {
        var exception = new DomainException(ErrorCodes.Send.InvalidTransition);

        Assert.Equal("send.invalid_transition", exception.Code);
        Assert.Equal("send.invalid_transition", exception.Message);
    }

    [Fact]
    public void Custom_message_does_not_replace_code()
    {
        var exception = new DomainException(ErrorCodes.Send.InvalidTransition, "Cannot mark a cancelled job as sending.");

        Assert.Equal("send.invalid_transition", exception.Code);
        Assert.Equal("Cannot mark a cancelled job as sending.", exception.Message);
    }
}
