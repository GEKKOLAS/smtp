using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Infrastructure.Jobs;

/// <summary>
/// Recurring sweep that promotes due scheduled sends to Queued and enqueues them
/// (spec 10-jobs.md J3).
/// </summary>
public sealed class PromoteScheduledSendsJob(
    IAppDbContext db, IBackgroundJobScheduler scheduler, IClock clock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.EmailSendJobs
            .Where(j => j.Status == SendJobStatus.Scheduled && j.ScheduledAt != null && j.ScheduledAt <= now)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var job in due)
        {
            job.Status = SendJobStatus.Queued;
            job.QueuedAt = now;
        }
        await db.SaveChangesAsync(ct);

        foreach (var job in due)
        {
            scheduler.EnqueueSend(job.Id);
        }
    }
}
