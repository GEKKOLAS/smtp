using MailTemplateHub.Application.Features.Sends;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MailTemplateHub.Api.Controllers;

[ApiController]
[Route("api/v1/sends")]
[Authorize]
public sealed class SendsController : ControllerBase
{
    public sealed record CreateSendRequest(
        Guid ConnectedEmailAccountId,
        Guid TemplateVersionId,
        IReadOnlyList<RecipientInput> Recipients,
        IReadOnlyDictionary<string, string?>? Variables,
        IReadOnlyList<SendAttachmentInput>? Attachments,
        DateTimeOffset? ScheduledAt);

    public sealed record TestSendRequest(
        Guid ConnectedEmailAccountId,
        Guid TemplateVersionId,
        IReadOnlyDictionary<string, string?>? Variables,
        IReadOnlyList<SendAttachmentInput>? Attachments,
        string? ToSelf);

    public sealed record RescheduleRequest(DateTimeOffset ScheduledAt);

    [HttpPost]
    [EnableRateLimiting("send")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Create(
        CreateSendRequest request, [FromServices] CreateSendJobHandler handler, CancellationToken ct)
    {
        var command = new CreateSendCommand(
            request.ConnectedEmailAccountId, request.TemplateVersionId, request.Recipients,
            request.Variables ?? new Dictionary<string, string?>(),
            request.Attachments ?? [],
            request.ScheduledAt,
            Request.Headers.TryGetValue("Idempotency-Key", out var key) ? key.ToString() : null);
        return Accepted(await handler.HandleAsync(command, ct));
    }

    [HttpPost("test")]
    [EnableRateLimiting("send")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TestSend(
        TestSendRequest request, [FromServices] TestSendHandler handler, CancellationToken ct)
    {
        var command = new TestSendCommand(
            request.ConnectedEmailAccountId, request.TemplateVersionId,
            request.Variables ?? new Dictionary<string, string?>(),
            request.Attachments ?? [],
            request.ToSelf ?? "login");
        return Accepted(await handler.HandleAsync(command, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] Guid? accountId, [FromQuery] Guid? templateId,
        [FromQuery] int page, [FromQuery] int pageSize,
        [FromServices] SendJobsHandler handler, CancellationToken ct)
        => Ok(await handler.ListAsync(
            new SendListQuery(status, accountId, templateId, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] SendJobsHandler handler, CancellationToken ct)
        => Ok(await handler.GetAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Cancel(Guid id, [FromServices] SendJobsHandler handler, CancellationToken ct)
        => Accepted(await handler.CancelAsync(id, ct));

    [HttpPost("{id:guid}/retry")]
    [EnableRateLimiting("send")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Retry(Guid id, [FromServices] SendJobsHandler handler, CancellationToken ct)
        => Accepted(await handler.RetryAsync(id, ct));

    [HttpPatch("{id:guid}/schedule")]
    public async Task<IActionResult> Reschedule(
        Guid id, RescheduleRequest request, [FromServices] SendJobsHandler handler, CancellationToken ct)
        => Ok(await handler.RescheduleAsync(id, request.ScheduledAt, ct));
}
