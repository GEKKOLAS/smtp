using MailTemplateHub.Application.Features.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Authorize]
public sealed class AuditLogsController(AuditLogsHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct)
        => Ok(await handler.ListAsync(
            new AuditLogQuery(action, from, to, page == 0 ? 1 : page, pageSize == 0 ? 30 : pageSize), ct));
}
