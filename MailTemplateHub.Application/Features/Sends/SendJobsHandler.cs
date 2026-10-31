using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Templates;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Sends;

public sealed record SendListQuery(string? Status, Guid? AccountId, Guid? TemplateId, int Page, int PageSize);

public sealed class SendJobsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IBackgroundJobScheduler jobs,
    IAuditWriter audit,
    IOptions<SendLimitsOptions> limitsOptions,
    IClock clock)
{
    public async Task<PagedSendJobs> ListAsync(SendListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.EmailSendJobs.Include(j => j.Recipients)
            .Where(j => j.UserId == currentUser.UserId);

        if (query.Status is not null && Enum.TryParse<SendJobStatus>(query.Status, ignoreCase: true, out var status))
        {
            q = q.Where(j => j.Status == status);
        }
        if (query.AccountId is { } accountId) q = q.Where(j => j.ConnectedEmailAccountId == accountId);
        if (query.TemplateId is { } templateId)
        {
            q = q.Where(j => db.EmailTemplateVersions.Any(v =>
                v.Id == j.TemplateVersionId && v.TemplateId == templateId));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var clickers = await UniqueClickersAsync(items, ct);
        return new PagedSendJobs(
            items.Select(j => SendJobDto.From(j, clickers.GetValueOrDefault(j.Id))).ToList(), page, pageSize, total);
    }

    public async Task<SendJobDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var job = await db.EmailSendJobs
            .Include(j => j.Recipients)
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        var events = await db.EmailProviderEvents
            .Where(e => e.SendJobId == id)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new ProviderEventDto(e.EventType, e.HttpStatus, e.ProviderErrorCode, e.CreatedAt))
            .ToListAsync(ct);

        var clickers = await UniqueClickersAsync([job], ct);
        return new SendJobDetailDto(
            SendJobDto.From(job, clickers.GetValueOrDefault(job.Id)),
            job.Recipients.OrderBy(r => r.CreatedAt).Select(RecipientDto.From).ToList(),
            events);
    }

    public async Task<SendJobDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadOwnedAsync(id, ct);
        if (!job.CanCancel) throw new ConflictException("send.not_cancellable", "This send can no longer be cancelled.");

        job.Cancel(clock.UtcNow);
        audit.Add(AuditActions.SendCancelled, currentUser.UserId, "email_send_job", id);
        await db.SaveChangesAsync(ct);
        return SendJobDto.From(job);
    }

    public async Task<SendJobDto> RetryAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadOwnedAsync(id, ct);
        if (job.Status is not (SendJobStatus.Failed or SendJobStatus.PartiallyFailed))
        {
            throw new ConflictException("send.not_retryable", "Only failed sends can be retried.");
        }

        var maxAttempts = limitsOptions.Value.MaxAttemptsPerRecipient;
        foreach (var recipient in job.Recipients.Where(r => r.Status == RecipientStatus.Failed))
        {
            recipient.Status = RecipientStatus.Pending;
            recipient.AttemptCount = 0;
            recipient.NextAttemptAt = null;
            recipient.FailureCode = null;
            recipient.FailureMessage = null;
        }
        _ = maxAttempts;

        job.Status = SendJobStatus.Queued;
        job.QueuedAt = clock.UtcNow;
        job.CompletedAt = null;
        job.FailureCode = null;
        job.FailureMessage = null;

        audit.Add(AuditActions.SendRetried, currentUser.UserId, "email_send_job", id);
        await db.SaveChangesAsync(ct);

        jobs.EnqueueSend(job.Id);
        // A partly failed send may already have clicks from the recipients it reached.
        var clickers = await UniqueClickersAsync([job], ct);
        return SendJobDto.From(job, clickers.GetValueOrDefault(job.Id));
    }

    public async Task<SendJobDto> RescheduleAsync(Guid id, DateTimeOffset scheduledAt, CancellationToken ct)
    {
        var job = await LoadOwnedAsync(id, ct);
        if (job.Status != SendJobStatus.Scheduled)
        {
            throw new ConflictException("send.not_reschedulable", "Only scheduled sends can be rescheduled.");
        }

        var limits = limitsOptions.Value;
        var now = clock.UtcNow;
        if (scheduledAt < now.AddMinutes(limits.MinScheduleMinutes) || scheduledAt > now.AddDays(limits.MaxScheduleDays))
        {
            throw new ValidationFailure("scheduledAt", "Invalid scheduled time.");
        }

        job.ScheduledAt = scheduledAt;
        await db.SaveChangesAsync(ct);
        return SendJobDto.From(job);
    }

    /// <summary>People who clicked, per tracked send; link-scanner clicks don't count.</summary>
    private async Task<Dictionary<Guid, int>> UniqueClickersAsync(IEnumerable<EmailSendJob> sends, CancellationToken ct)
    {
        var tracked = sends.Where(j => j.TrackClicks).Select(j => j.Id).ToList();
        if (tracked.Count == 0) return [];

        return await db.TrackedLinks
            .Where(l => tracked.Contains(l.SendJobId) && l.Clicks.Any(c => c.Classification == ClickClassifications.Human))
            .GroupBy(l => l.SendJobId)
            .Select(g => new { SendJobId = g.Key, Count = g.Select(l => l.RecipientId).Distinct().Count() })
            .ToDictionaryAsync(x => x.SendJobId, x => x.Count, ct);
    }

    private async Task<EmailSendJob> LoadOwnedAsync(Guid id, CancellationToken ct) =>
        await db.EmailSendJobs
            .Include(j => j.Recipients)
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();
}
