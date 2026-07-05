using MailTemplateHub.Application.Abstractions.Email;

namespace MailTemplateHub.Infrastructure.Providers.Microsoft;

/// <summary>Maps Microsoft Graph send failures to a normalized <see cref="ProviderErrorKind"/> (spec 07 §3.2).</summary>
internal static class GraphErrorMap
{
    public static ProviderErrorKind Classify(int? httpStatus, string? code)
    {
        code = code?.ToLowerInvariant();

        return code switch
        {
            "invalidauthenticationtoken" => ProviderErrorKind.AuthExpired,
            "erroraccessdenied" => ProviderErrorKind.InsufficientScope,
            "errormessagesizeexceeded" => ProviderErrorKind.MessageTooLarge,
            "errorinvalidrecipients" => ProviderErrorKind.RecipientRejected,
            "errorsendasdenied" => ProviderErrorKind.PermanentOther,
            "errormessagesubmissionblocked" or "errorquotaexceeded" => ProviderErrorKind.QuotaExceeded,
            "mailboxconcurrency" => ProviderErrorKind.Transient,
            _ => httpStatus switch
            {
                401 => ProviderErrorKind.AuthExpired,
                403 => ProviderErrorKind.InsufficientScope,
                413 => ProviderErrorKind.MessageTooLarge,
                429 => ProviderErrorKind.Transient,
                >= 500 => ProviderErrorKind.Transient,
                _ => ProviderErrorKind.PermanentOther,
            },
        };
    }

    public static string SafeMessage(ProviderErrorKind kind) => kind switch
    {
        ProviderErrorKind.AuthExpired => "The Outlook access token expired.",
        ProviderErrorKind.AuthRevoked => "Outlook access was revoked; reconnect the account.",
        ProviderErrorKind.InsufficientScope => "The Outlook connection lacks send permission.",
        ProviderErrorKind.MessageTooLarge => "The message exceeds Outlook's size limit.",
        ProviderErrorKind.RecipientRejected => "Outlook rejected a recipient address.",
        ProviderErrorKind.QuotaExceeded => "Outlook sending limit reached; try again later.",
        ProviderErrorKind.Transient => "Outlook was temporarily unavailable.",
        _ => "Outlook could not send the message.",
    };
}
