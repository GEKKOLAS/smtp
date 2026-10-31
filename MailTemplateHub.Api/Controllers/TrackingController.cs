using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class TrackingController(TrackingHandler handler) : ControllerBase
{
    private const string GonePage =
        """
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Link not available</title></head>
        <body style="font-family:system-ui,sans-serif;max-width:32rem;margin:15vh auto;padding:0 1rem;color:#222">
        <h1 style="font-size:1.25rem">This link isn't available</h1>
        <p>It may have expired. If you got it in an email, reply to the sender to ask for it again.</p>
        </body></html>
        """;

    /// <summary>
    /// The redirect recipients' clicks go through. Anonymous by design: the
    /// recipient has no account. The destination comes from the stored link only.
    /// </summary>
    [HttpGet("t/{token}")]
    [HttpHead("t/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("tracking")]
    public async Task<IActionResult> Follow(string token, CancellationToken ct)
    {
        var target = await handler.FollowAsync(
            token, Request.Headers.UserAgent.ToString(), record: !HttpMethods.IsHead(Request.Method), ct);

        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (target is null)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status404NotFound,
                ContentType = "text/html; charset=utf-8",
                Content = GonePage,
            };
        }
        return Redirect(target);
    }

    /// <summary>Whether click tracking can be offered (a public base URL is configured).</summary>
    [HttpGet("tracking/status")]
    [Authorize]
    public IActionResult Status([FromServices] IOptions<TrackingOptions> options)
        => Ok(new { clickTracking = options.Value.IsConfigured });
}
