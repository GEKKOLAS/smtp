using MailTemplateHub.Application.Features.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public sealed class AiController(GenerateTemplateHandler handler) : ControllerBase
{
    public sealed record GenerateRequest(
        string Prompt,
        string? BrandColor,
        string? Tone,
        IReadOnlyList<Guid>? AssetIds,
        IReadOnlyList<string>? Variables);

    /// <summary>Generates an MJML template from a prompt (returns content + preview, no persist).</summary>
    [HttpPost("templates/generate")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Generate(GenerateRequest request, CancellationToken ct)
        => Ok(await handler.HandleAsync(
            new GenerateTemplateCommand(
                request.Prompt, request.BrandColor, request.Tone,
                request.AssetIds ?? [], request.Variables ?? []),
            ct));
}
