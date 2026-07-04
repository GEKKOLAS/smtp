namespace MailTemplateHub.Application.Common;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string SessionCookieName { get; init; } = "mth_session";
    public string CsrfCookieName { get; init; } = "mth_csrf";
    public string CsrfHeaderName { get; init; } = "X-CSRF-Token";

    /// <summary>Prod must be true (with a __Host- cookie name); dev over http keeps false.</summary>
    public bool SecureCookies { get; init; }

    public int SessionAbsoluteDays { get; init; } = 30;
    public int SessionIdleDays { get; init; } = 7;
    public int ResetTokenMinutes { get; init; } = 30;

    public TimeSpan SessionAbsoluteLifetime => TimeSpan.FromDays(SessionAbsoluteDays);
    public TimeSpan SessionIdleTimeout => TimeSpan.FromDays(SessionIdleDays);
}
