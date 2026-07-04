using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Api.Auth;

/// <summary>Issues and clears the session (HttpOnly) and CSRF (JS-readable) cookies.</summary>
public sealed class AuthCookies(IOptions<AuthOptions> authOptions)
{
    public void Issue(HttpResponse response, string sessionToken, string csrfToken)
    {
        var auth = authOptions.Value;
        response.Cookies.Append(auth.SessionCookieName, sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = auth.SecureCookies,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = auth.SessionAbsoluteLifetime,
        });
        response.Cookies.Append(auth.CsrfCookieName, csrfToken, new CookieOptions
        {
            HttpOnly = false, // double-submit: the SPA reads it and echoes the header
            Secure = auth.SecureCookies,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = auth.SessionAbsoluteLifetime,
        });
    }

    public void Clear(HttpResponse response)
    {
        var auth = authOptions.Value;
        response.Cookies.Delete(auth.SessionCookieName, new CookieOptions { Path = "/" });
        response.Cookies.Delete(auth.CsrfCookieName, new CookieOptions { Path = "/" });
    }
}
