using MailTemplateHub.Application.Features.Inbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

/// <summary>
/// Reads a connected mailbox so the user can answer a real conversation with a
/// template. Nothing is stored: every call goes to the provider live.
/// </summary>
[ApiController]
[Route("api/v1/inbox")]
[Authorize]
public sealed class InboxController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid accountId,
        [FromQuery] string? pageToken,
        [FromQuery] string? search,
        [FromQuery] int pageSize,
        [FromServices] InboxHandler handler,
        CancellationToken ct)
        => Ok(await handler.ListAsync(new InboxListQuery(accountId, pageToken, search, pageSize), ct));

    [HttpGet("{messageId}")]
    public async Task<IActionResult> Get(
        string messageId,
        [FromQuery] Guid accountId,
        [FromServices] InboxHandler handler,
        CancellationToken ct)
        => Ok(await handler.GetAsync(accountId, messageId, ct));
}
