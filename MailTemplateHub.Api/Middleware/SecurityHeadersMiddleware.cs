namespace MailTemplateHub.Api.Middleware;

/// <summary>
/// Baseline security response headers (spec 04-security.md §9). HSTS is only sent
/// over HTTPS. The sandboxed preview route sets its own CSP and is exempt from
/// the strict frame policy.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Frame-Options"] = "DENY";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await next(context);
    }
}
