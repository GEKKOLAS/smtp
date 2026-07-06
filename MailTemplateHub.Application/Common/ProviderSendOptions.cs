namespace MailTemplateHub.Application.Common;

/// <summary>
/// Provider send endpoints. Configurable so tests (and future regions) can point
/// them at a double instead of the live APIs.
/// </summary>
public sealed class ProviderSendOptions
{
    public const string SectionName = "ProviderSend";

    public string GmailSendUrl { get; init; } = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";
    public string GraphSendUrl { get; init; } = "https://graph.microsoft.com/v1.0/me/sendMail";
}
