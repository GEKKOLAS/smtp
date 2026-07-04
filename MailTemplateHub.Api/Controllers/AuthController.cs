using MailTemplateHub.Api.Auth;
using MailTemplateHub.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthCookies cookies) : ControllerBase
{
    public sealed record RegisterRequest(string Email, string Password, string DisplayName);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Token, string NewPassword);

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        RegisterRequest request, [FromServices] RegisterHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new RegisterCommand(request.Email, request.Password, request.DisplayName), ct);

        if (result is { SessionToken: not null, CsrfToken: not null })
        {
            cookies.Issue(Response, result.SessionToken, result.CsrfToken);
        }
        return StatusCode(StatusCodes.Status201Created, new { user = result.User });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        LoginRequest request, [FromServices] LoginHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);
        cookies.Issue(Response, result.SessionToken!, result.CsrfToken!);
        return Ok(new { user = result.User });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromServices] SessionsHandler handler, CancellationToken ct)
    {
        await handler.LogoutAsync(ct);
        cookies.Clear(Response);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll([FromServices] SessionsHandler handler, CancellationToken ct)
    {
        await handler.LogoutAllAsync(ct);
        cookies.Clear(Response);
        return NoContent();
    }

    [HttpPost("password/forgot")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request, [FromServices] PasswordResetHandler handler, CancellationToken ct)
    {
        await handler.RequestAsync(new ForgotPasswordCommand(request.Email), ct);
        return Accepted(value: new { });
    }

    [HttpPost("password/reset")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request, [FromServices] PasswordResetHandler handler, CancellationToken ct)
    {
        await handler.ResetAsync(new ResetPasswordCommand(request.Token, request.NewPassword), ct);
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> Sessions([FromServices] SessionsHandler handler, CancellationToken ct)
        => Ok(new { items = await handler.ListAsync(ct) });

    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(
        Guid id, [FromServices] SessionsHandler handler, CancellationToken ct)
    {
        await handler.RevokeAsync(id, ct);
        return NoContent();
    }
}
