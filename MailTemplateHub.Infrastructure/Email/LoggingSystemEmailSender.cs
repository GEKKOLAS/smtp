using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Email;

/// <summary>
/// Dev/test stand-in: writes a ready-to-open reset link to the log instead of
/// sending mail. Opt-in via <c>Auth:LogPasswordResetLinks</c> so it can never be
/// the accidental production default. A real transactional sender replaces this
/// at deployment (Phase 7).
/// </summary>
public sealed class LoggingSystemEmailSender(
    IOptions<OAuthGeneralOptions> oauthOptions,
    ILogger<LoggingSystemEmailSender> logger) : ISystemEmailSender
{
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        // The property is named ResetUrl, not "token": SecretScrubbingEnricher
        // redacts any property called "token", which would make the link useless.
        // Printing it is the whole point of this sender, and it only ever runs
        // where an operator has explicitly opted in.
        var resetUrl = $"{oauthOptions.Value.FrontendBaseUrl.TrimEnd('/')}"
                       + $"/reset-password?token={Uri.EscapeDataString(token)}";
        logger.LogInformation("Password reset requested for {Email}. Open: {ResetUrl}", email, resetUrl);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Default when no transactional sender is configured. The reset endpoint always
/// answers 202 (it must not reveal whether an address exists), so a missing
/// sender would otherwise be a silent no-op — this makes the gap loud in logs.
/// </summary>
public sealed class UnconfiguredSystemEmailSender(ILogger<UnconfiguredSystemEmailSender> logger)
    : ISystemEmailSender
{
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        logger.LogError(
            "No transactional email sender is configured; the password reset for {Email} was dropped.", email);
        return Task.CompletedTask;
    }
}
