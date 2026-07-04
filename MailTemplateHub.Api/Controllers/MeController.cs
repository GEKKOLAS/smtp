using MailTemplateHub.Application.Features.Me;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    public sealed record UpdateProfileRequest(string DisplayName);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpGet]
    public async Task<IActionResult> Get([FromServices] MeHandler handler, CancellationToken ct)
        => Ok(await handler.GetAsync(ct));

    [HttpPatch]
    public async Task<IActionResult> UpdateProfile(
        UpdateProfileRequest request, [FromServices] MeHandler handler, CancellationToken ct)
        => Ok(await handler.UpdateProfileAsync(new UpdateProfileCommand(request.DisplayName), ct));

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request, [FromServices] MeHandler handler, CancellationToken ct)
    {
        await handler.ChangePasswordAsync(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }
}
