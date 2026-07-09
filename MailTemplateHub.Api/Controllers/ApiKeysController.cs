using System.Security.Claims;
using MailTemplateHub.Application.Features.ApiKeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize]
public sealed class ApiKeysController(ApiKeysHandler handler) : ControllerBase, IActionFilter
{
    public sealed record CreateRequest(string Name, int? ExpiresInDays);

    // Key management is browser-session only — an API key cannot mint or revoke keys.
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (User.FindFirstValue("mth:auth") == "api_key")
        {
            context.Result = Forbid();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { items = await handler.ListAsync(ct) });

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct)
    {
        var created = await handler.CreateAsync(new CreateApiKeyCommand(request.Name, request.ExpiresInDays), ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await handler.RevokeAsync(id, ct);
        return NoContent();
    }
}
