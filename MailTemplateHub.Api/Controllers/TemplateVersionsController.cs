using MailTemplateHub.Application.Features.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/templates/{templateId:guid}/versions")]
[Authorize]
public sealed class TemplateVersionsController(TemplateVersionsHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid templateId, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct)
        => Ok(await handler.ListAsync(templateId, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, ct));

    [HttpGet("{versionId:guid}")]
    public async Task<IActionResult> Get(Guid templateId, Guid versionId, CancellationToken ct)
        => Ok(await handler.GetAsync(templateId, versionId, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Save(Guid templateId, TemplateContentInput content, CancellationToken ct)
    {
        var version = await handler.SaveAsync(templateId, content, ct);
        return StatusCode(StatusCodes.Status201Created, version);
    }

    [HttpPost("{versionId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Restore(Guid templateId, Guid versionId, CancellationToken ct)
    {
        var version = await handler.RestoreAsync(templateId, versionId, ct);
        return StatusCode(StatusCodes.Status201Created, version);
    }
}
