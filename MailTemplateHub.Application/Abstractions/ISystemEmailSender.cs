namespace MailTemplateHub.Application.Abstractions;

/// <summary>
/// Transactional system mail (password reset, verification). Distinct from the
/// user-facing provider send pipeline; dev implementation just logs.
/// </summary>
public interface ISystemEmailSender
{
    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken);
}
