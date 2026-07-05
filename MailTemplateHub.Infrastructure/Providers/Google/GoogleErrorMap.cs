using MailTemplateHub.Application.Abstractions.Email;

namespace MailTemplateHub.Infrastructure.Providers.Google;

/// <summary>Maps Gmail send failures to a normalized <see cref="ProviderErrorKind"/> (spec 07 §3.1).</summary>
internal static class GoogleErrorMap
{
    public static ProviderErrorKind Classify(int? httpStatus, string? reason)
    {
        reason = reason?.ToLowerInvariant();

        if (reason is "rmailexceededdailylimit" or "dailylimitexceeded" || reason == "user rate limit exceeded")
            return ProviderErrorKind.QuotaExceeded;

        return httpStatus switch
        {
            401 => ProviderErrorKind.AuthExpired,
            403 when reason is "insufficientpermissions" or "insufficient permission" => ProviderErrorKind.InsufficientScope,
            403 when reason is "ratelimitexceeded" or "userratelimitexceeded" => ProviderErrorKind.Transient,
            403 when reason is "dailylimitexceeded" => ProviderErrorKind.QuotaExceeded,
            429 => ProviderErrorKind.Transient,
            413 => ProviderErrorKind.MessageTooLarge,
            400 when reason is "invalidargument" => ProviderErrorKind.RecipientRejected,
            >= 500 => ProviderErrorKind.Transient,
            _ => ProviderErrorKind.PermanentOther,
        };
    }

    public static string SafeMessage(ProviderErrorKind kind) => kind switch
    {
        ProviderErrorKind.AuthExpired => "The Gmail access token expired.",
        ProviderErrorKind.AuthRevoked => "Gmail access was revoked; reconnect the account.",
        ProviderErrorKind.InsufficientScope => "The Gmail connection lacks send permission.",
        ProviderErrorKind.MessageTooLarge => "The message exceeds Gmail's size limit.",
        ProviderErrorKind.RecipientRejected => "Gmail rejected a recipient address.",
        ProviderErrorKind.QuotaExceeded => "Gmail sending quota reached; try again later.",
        ProviderErrorKind.Transient => "Gmail was temporarily unavailable.",
        _ => "Gmail could not send the message.",
    };
}
