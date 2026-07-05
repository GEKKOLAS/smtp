using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Infrastructure.Providers.Google;
using MailTemplateHub.Infrastructure.Providers.Microsoft;

namespace MailTemplateHub.UnitTests.Sending;

public class ProviderErrorMapTests
{
    [Theory]
    [InlineData(401, null, ProviderErrorKind.AuthExpired)]
    [InlineData(403, "insufficientPermissions", ProviderErrorKind.InsufficientScope)]
    [InlineData(403, "rateLimitExceeded", ProviderErrorKind.Transient)]
    [InlineData(403, "dailyLimitExceeded", ProviderErrorKind.QuotaExceeded)]
    [InlineData(429, null, ProviderErrorKind.Transient)]
    [InlineData(413, null, ProviderErrorKind.MessageTooLarge)]
    [InlineData(400, "invalidArgument", ProviderErrorKind.RecipientRejected)]
    [InlineData(500, null, ProviderErrorKind.Transient)]
    [InlineData(404, null, ProviderErrorKind.PermanentOther)]
    public void Gmail_maps_errors(int status, string? reason, ProviderErrorKind expected)
    {
        Assert.Equal(expected, GoogleErrorMap.Classify(status, reason));
    }

    [Theory]
    [InlineData(401, "InvalidAuthenticationToken", ProviderErrorKind.AuthExpired)]
    [InlineData(403, "ErrorAccessDenied", ProviderErrorKind.InsufficientScope)]
    [InlineData(413, "ErrorMessageSizeExceeded", ProviderErrorKind.MessageTooLarge)]
    [InlineData(400, "ErrorInvalidRecipients", ProviderErrorKind.RecipientRejected)]
    [InlineData(503, "MailboxConcurrency", ProviderErrorKind.Transient)]
    [InlineData(429, null, ProviderErrorKind.Transient)]
    [InlineData(400, "ErrorQuotaExceeded", ProviderErrorKind.QuotaExceeded)]
    [InlineData(400, null, ProviderErrorKind.PermanentOther)]
    public void Graph_maps_errors(int status, string? code, ProviderErrorKind expected)
    {
        Assert.Equal(expected, GraphErrorMap.Classify(status, code));
    }
}
