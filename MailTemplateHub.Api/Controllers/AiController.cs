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
        IReadOnlyList<string>? Variables,
        string? VideoUrl,
        bool UseAdvancedModel = false,
        string? CurrentMjml = null,
        string? CurrentHtml = null,
        Guid? BackgroundImageAssetId = null,
        Guid? HeaderLogoAssetId = null,
        Guid? FooterLogoAssetId = null);

    /// <summary>
    /// Generates an MJML template from a prompt (returns content + preview, no persist).
    /// When <see cref="GenerateRequest.CurrentMjml"/> or <see cref="GenerateRequest.CurrentHtml"/>
    /// is set, the prompt is treated as an edit instruction against that existing template
    /// instead of a from-scratch brief.
    /// </summary>
    [HttpPost("templates/generate")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Generate(GenerateRequest request, CancellationToken ct)
        => Ok(await handler.HandleAsync(
            new GenerateTemplateCommand(
                request.Prompt, request.BrandColor, request.Tone,
                request.AssetIds ?? [], request.Variables ?? [], request.VideoUrl,
                request.UseAdvancedModel, request.CurrentMjml, request.CurrentHtml,
                request.BackgroundImageAssetId, request.HeaderLogoAssetId, request.FooterLogoAssetId),
            ct));
}
