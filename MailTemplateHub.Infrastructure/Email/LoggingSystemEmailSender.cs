using MailTemplateHub.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace MailTemplateHub.Infrastructure.Email;

/// <summary>
/// Dev/test stand-in: writes the reset link to the log instead of sending mail.
/// A real transactional sender replaces this at deployment (Phase 7).
/// </summary>
public sealed class LoggingSystemEmailSender(ILogger<LoggingSystemEmailSender> logger) : ISystemEmailSender
{
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        logger.LogInformation("Password reset requested for {Email}; token: {Token}", email, token);
        return Task.CompletedTask;
    }
}
