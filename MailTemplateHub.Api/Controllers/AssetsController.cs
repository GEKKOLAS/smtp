using MailTemplateHub.Application.Features.Assets;
using MailTemplateHub.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public sealed class AssetsController : ControllerBase
{
    public sealed record RequestUploadRequest(string Filename, string MimeType, long SizeBytes);
    public sealed record VisibilityRequest(string Access);

    [HttpPost("uploads")]
    [EnableRateLimiting("upload")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestUpload(
        RequestUploadRequest request, [FromServices] RequestUploadHandler handler, CancellationToken ct)
    {
        var grant = await handler.HandleAsync(
            new RequestUploadCommand(request.Filename, request.MimeType, request.SizeBytes), ct);
        return StatusCode(StatusCodes.Status201Created, grant);
    }

    [HttpPost("uploads/{assetId:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(
        Guid assetId, [FromServices] CompleteUploadHandler handler, CancellationToken ct)
        => Ok(await handler.HandleAsync(assetId, ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? kind,
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] AssetsHandler handler,
        CancellationToken ct)
        => Ok(await handler.ListAsync(
            new AssetListQuery(kind, search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] AssetsHandler handler, CancellationToken ct)
        => Ok(await handler.GetAsync(id, ct));

    [HttpGet("{id:guid}/download-url")]
    public async Task<IActionResult> DownloadUrl(Guid id, [FromServices] AssetsHandler handler, CancellationToken ct)
        => Ok(await handler.GetDownloadUrlAsync(id, ct));

    [HttpPost("{id:guid}/visibility")]
    public async Task<IActionResult> SetVisibility(
        Guid id, VisibilityRequest request, [FromServices] AssetsHandler handler, CancellationToken ct)
    {
        if (!Enum.TryParse<AssetAccess>(request.Access, ignoreCase: true, out var access))
        {
            return UnprocessableEntity(new { errorCode = "asset.invalid_access" });
        }
        return Ok(await handler.SetVisibilityAsync(id, access, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, [FromQuery] bool force, [FromServices] AssetsHandler handler, CancellationToken ct)
    {
        await handler.DeleteAsync(id, force, ct);
        return NoContent();
    }
}
