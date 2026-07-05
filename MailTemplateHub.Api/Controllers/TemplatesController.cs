using MailTemplateHub.Application.Features.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public sealed class TemplatesController(TemplatesHandler handler) : ControllerBase
{
    public sealed record CreateRequest(string Name, string? Description, TemplateContentInput Content);
    public sealed record UpdateRequest(string? Name, string? Description);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] bool archived,
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct)
        => Ok(await handler.ListAsync(
            new TemplateListQuery(search, archived, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await handler.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct)
    {
        var template = await handler.CreateAsync(
            new CreateTemplateCommand(request.Name, request.Description, request.Content), ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, template);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRequest request, CancellationToken ct)
        => Ok(await handler.UpdateAsync(id, new UpdateTemplateCommand(request.Name, request.Description), ct));

    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var template = await handler.DuplicateAsync(id, ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, template);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await handler.SetArchivedAsync(id, true, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken ct)
    {
        await handler.SetArchivedAsync(id, false, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await handler.DeleteAsync(id, ct);
        return NoContent();
    }
}
