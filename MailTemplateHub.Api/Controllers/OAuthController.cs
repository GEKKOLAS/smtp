using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Accounts;
using MailTemplateHub.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/oauth/{provider}")]
[Authorize]
public sealed class OAuthController(IOptions<OAuthGeneralOptions> generalOptions, ILogger<OAuthController> logger)
    : ControllerBase
{
    // Provider slug -> enum; anything else is a 404 route miss.
    private static bool TryParseProvider(string provider, out EmailProvider parsed)
    {
        parsed = default;
        switch (provider.ToLowerInvariant())
        {
            case "gmail": parsed = EmailProvider.Gmail; return true;
            case "outlook": parsed = EmailProvider.Outlook; return true;
            default: return false;
        }
    }

    [HttpGet("start")]
    [EnableRateLimiting("oauth")]
    public async Task<IActionResult> Start(
        string provider,
        [FromQuery] string? returnTo,
        [FromServices] ConnectStartHandler handler,
        CancellationToken ct)
    {
        if (!TryParseProvider(provider, out var parsed)) return NotFound();
        var result = await handler.HandleAsync(parsed, returnTo, ct);
        return Ok(new { authorizationUrl = result.AuthorizationUrl });
    }

    // The provider redirects the browser here (top-level GET), so this returns a
    // 302 back to the SPA rather than JSON. No token material ever appears in the URL.
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        [FromServices] OAuthCallbackHandler handler,
        CancellationToken ct)
    {
        if (!TryParseProvider(provider, out var parsed)) return NotFound();

        var frontendBase = generalOptions.Value.FrontendBaseUrl;

        // The user denied consent at the provider — or the provider rejected the
        // request outright (bad redirect URI, app not yet propagated, permissions
        // not granted, etc). Log the raw reason: the frontend only ever shows a
        // generic "cancelled" message for any non-empty `error`, so this is the
        // only place the real cause is visible.
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning(
                "OAuth callback for {Provider} returned an error: {Error} — {ErrorDescription}",
                provider, error, error_description);
            return RedirectToFrontend(frontendBase, "/accounts", errorCode: "oauth.access_denied");
        }

        var redirectUri = ConnectStartHandler.BuildRedirectUri(generalOptions.Value.RedirectBaseUrl, parsed);
        try
        {
            var result = await handler.HandleAsync(parsed, code, state, redirectUri, ct);
            var query = result.ScopeMissing
                ? $"?error=oauth.scope_missing"
                : $"?connected={provider.ToLowerInvariant()}";
            return Redirect($"{frontendBase.TrimEnd('/')}{result.ReturnTo}{query}");
        }
        catch (OAuthCallbackException ex)
        {
            return RedirectToFrontend(frontendBase, "/accounts", ex.ErrorCode);
        }
    }

    private RedirectResult RedirectToFrontend(string baseUrl, string path, string errorCode) =>
        Redirect($"{baseUrl.TrimEnd('/')}{path}?error={Uri.EscapeDataString(errorCode)}");
}
