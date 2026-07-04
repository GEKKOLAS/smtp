using MailTemplateHub.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Api.Middleware;

/// <summary>
/// Double-submit CSRF check (spec 04-security.md §1): any non-GET request that
/// carries a session cookie must echo the CSRF cookie's value in the header.
/// Anonymous requests (no session cookie) pass — there is no session to forge —
/// and are protected by rate limiting instead.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next, IOptions<AuthOptions> authOptions)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    public async Task InvokeAsync(HttpContext context)
    {
        var auth = authOptions.Value;

        if (SafeMethods.Contains(context.Request.Method)
            || !context.Request.Cookies.ContainsKey(auth.SessionCookieName))
        {
            await next(context);
            return;
        }

        var cookie = context.Request.Cookies[auth.CsrfCookieName];
        var header = context.Request.Headers[auth.CsrfHeaderName].ToString();

        if (string.IsNullOrEmpty(cookie) || !string.Equals(cookie, header, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "CSRF token missing or invalid.",
                Extensions = { ["errorCode"] = "csrf.invalid" },
            });
            return;
        }

        await next(context);
    }
}
