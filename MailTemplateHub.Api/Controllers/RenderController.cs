using MailTemplateHub.Application.Features.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/render")]
[Authorize]
public sealed class RenderController(RenderHandler handler) : ControllerBase
{
    [HttpPost("preview")]
    [EnableRateLimiting("render")]
    public async Task<IActionResult> Preview(PreviewRequest request, CancellationToken ct)
        => Ok(await handler.PreviewAsync(request, ct));

    [HttpPost("validate")]
    [EnableRateLimiting("render")]
    public async Task<IActionResult> Validate(PreviewRequest request, CancellationToken ct)
        => Ok(await handler.ValidateAsync(request, ct));
}
