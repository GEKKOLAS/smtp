namespace MailTemplateHub.Application.Common;

/// <summary>
/// Transactional system mail via Resend (password reset, verification). Separate
/// from the user's own connected mailboxes: system mail must not depend on a
/// user's OAuth grant — that grant is exactly what a locked-out user cannot use.
/// When the key is empty the sender is disabled and dev falls back to logging
/// the link (<c>Auth:LogPasswordResetLinks</c>).
/// </summary>
public sealed class EmailSenderOptions
{
    public const string SectionName = "EmailSender";

    /// <summary>Resend API key. When empty, transactional sending is disabled.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Must be on a domain verified in Resend, or Resend rejects the send.</summary>
    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Mail Template Hub";
    public string ApiUrl { get; init; } = "https://api.resend.com/emails";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress);
}
