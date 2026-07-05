using System.Text.Json;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Templates;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Sends;

/// <summary>
/// Sends a test to the user's own address using the identical pipeline as a real
/// send, with a [TEST] subject prefix (spec 06 §9, 08 §4).
/// </summary>
public sealed class TestSendHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITemplateRenderer renderer,
    IBackgroundJobScheduler jobs,
    IAuditWriter audit,
    IClock clock)
{
    public async Task<SendJobDto> HandleAsync(TestSendCommand command, CancellationToken ct)
    {
        var account = await db.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a => a.Id == command.ConnectedEmailAccountId && a.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        if (account.State != AccountState.Active)
        {
            throw new ConflictException("send.account_needs_reconnect", "This account needs to be reconnected.");
        }

        var version = await db.EmailTemplateVersions
            .Include(v => v.TemplateAssets)
            .Include(v => v.Template!)
            .FirstOrDefaultAsync(v => v.Id == command.TemplateVersionId && v.Template!.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        var toEmail = string.Equals(command.ToSelf, "account", StringComparison.OrdinalIgnoreCase)
            ? account.EmailAddress
            : await db.Users.Where(u => u.Id == currentUser.UserId).Select(u => u.Email).FirstAsync(ct);

        var content = TemplateContentMapping.ToRenderContent(version);
        var rendered = renderer.Render(new RenderRequest(content, command.Variables, Strict: false,
            new Dictionary<Guid, string>()));
        var now = clock.UtcNow;

        var job = new EmailSendJob
        {
            UserId = currentUser.UserId,
            ConnectedEmailAccountId = account.Id,
            TemplateVersionId = version.Id,
            Status = SendJobStatus.Queued,
            IsTest = true,
            SubjectSnapshot = $"[TEST] {rendered.Subject}",
            VariableValues = JsonSerializer.SerializeToDocument(command.Variables),
            QueuedAt = now,
            Recipients =
            [
                new EmailSendRecipient
                {
                    SendJobId = default,
                    EmailAddress = toEmail,
                    DisplayName = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            ],
        };
        db.EmailSendJobs.Add(job);
        audit.Add(AuditActions.SendCreated, currentUser.UserId, "email_send_job", job.Id, new { test = true });
        await db.SaveChangesAsync(ct);

        jobs.EnqueueSend(job.Id);
        return SendJobDto.From(job);
    }
}
