using MailTemplateHub.Application.Features.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class AnalyticsController(AnalyticsHandler handler) : ControllerBase
{
    /// <summary>Totals, per-day series, per-template rows and top links for the last 7, 30 or 90 days.</summary>
    [HttpGet("analytics/summary")]
    public async Task<IActionResult> Summary([FromQuery] int days, CancellationToken ct)
        => Ok(await handler.SummaryAsync(days == 0 ? 30 : days, ct));

    /// <summary>Per-link and per-recipient results for one send.</summary>
    [HttpGet("sends/{id:guid}/analytics")]
    public async Task<IActionResult> ForSend(Guid id, CancellationToken ct)
        => Ok(await handler.ForSendAsync(id, ct));
}
