using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Analytics;

/// <summary>
/// The honest numbers of spec 14 F4: "accepted" is what the provider took, not
/// "delivered"; bounces come from delivery reports; click rate counts people,
/// not link scanners, over tracked sends only. There is no open rate.
/// </summary>
public sealed record AnalyticsTotals(
    int Sends, int Recipients, int Accepted, int Failed, int Bounced,
    int TrackedRecipients, int UniqueClickers, double? BounceRate, double? ClickRate);

public sealed record DailyPoint(DateOnly Date, int Accepted, int Failed, int Bounced);

public sealed record TemplateRow(
    Guid TemplateId, string Name, int Sends, int Recipients, int Accepted, int Bounced,
    double? BounceRate, int TrackedRecipients, int UniqueClickers, double? ClickRate);

public sealed record LinkRow(string Url, int Clicks, int UniqueClickers);

public sealed record AnalyticsSummaryDto(
    DateTimeOffset From, DateTimeOffset To, AnalyticsTotals Totals,
    IReadOnlyList<DailyPoint> Daily, IReadOnlyList<TemplateRow> Templates, IReadOnlyList<LinkRow> TopLinks);

public sealed record SendLinkRow(int Position, string Url, int Clicks, int UniqueClickers, int ScannerClicks);

public sealed record SendRecipientRow(Guid RecipientId, string Email, string Status, bool Clicked, DateTimeOffset? FirstClickAt);

public sealed record SendAnalyticsDto(
    bool TrackClicks, string? UtmCampaign, AnalyticsTotals Totals,
    IReadOnlyList<SendLinkRow> Links, IReadOnlyList<SendRecipientRow> Recipients);

public sealed class AnalyticsHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public static readonly IReadOnlyList<int> AllowedDays = [7, 30, 90];

    private sealed record JobRow(Guid Id, bool TrackClicks, Guid TemplateId, string? TemplateName);

    private sealed record RecipientRow(Guid Id, Guid SendJobId, string Email, RecipientStatus Status, DateTimeOffset? SentAt);

    private sealed record LinkStat(
        Guid SendJobId, Guid RecipientId, int Position, string TargetUrl, int Human, int Scanner, DateTimeOffset? FirstHuman);

    public async Task<AnalyticsSummaryDto> SummaryAsync(int days, CancellationToken ct)
    {
        if (!AllowedDays.Contains(days)) days = 30;
        var to = clock.UtcNow;
        var from = to.AddDays(-days);

        // Deleted templates still name their history, so query filters are off; ownership is explicit.
        var jobs = await db.EmailSendJobs.IgnoreQueryFilters()
            .Where(j => j.UserId == currentUser.UserId && !j.IsTest && j.CreatedAt >= from)
            .Select(j => new JobRow(j.Id, j.TrackClicks, j.TemplateVersion!.TemplateId, j.TemplateVersion.Template!.Name))
            .ToListAsync(ct);
        var jobIds = jobs.Select(j => j.Id).ToList();
        var recipients = await LoadRecipientsAsync(jobIds, ct);
        var links = await LoadLinksAsync(jobIds, ct);

        var daily = Enumerable.Range(0, days + 1)
            .Select(offset => DateOnly.FromDateTime(from.UtcDateTime.Date.AddDays(offset)))
            .Select(date =>
            {
                var day = recipients.Where(r => r.SentAt is { } s && DateOnly.FromDateTime(s.UtcDateTime) == date).ToList();
                return new DailyPoint(date,
                    day.Count(r => r.Status == RecipientStatus.Sent),
                    day.Count(r => r.Status == RecipientStatus.Failed),
                    day.Count(r => r.Status == RecipientStatus.Bounced));
            })
            .ToList();

        var templates = jobs
            .GroupBy(j => j.TemplateId)
            .Select(group =>
            {
                var ids = group.Select(j => j.Id).ToHashSet();
                var totals = Totals(group.ToList(), recipients.Where(r => ids.Contains(r.SendJobId)).ToList(),
                    links.Where(l => ids.Contains(l.SendJobId)).ToList());
                return new TemplateRow(group.Key, group.First().TemplateName ?? "Deleted template", totals.Sends,
                    totals.Recipients, totals.Accepted, totals.Bounced, totals.BounceRate,
                    totals.TrackedRecipients, totals.UniqueClickers, totals.ClickRate);
            })
            .OrderByDescending(t => t.Recipients)
            .ToList();

        var topLinks = links
            .GroupBy(l => DisplayUrl(l.TargetUrl))
            .Select(g => new LinkRow(g.Key, g.Sum(l => l.Human), g.Where(l => l.Human > 0).Select(l => l.RecipientId).Distinct().Count()))
            .Where(l => l.Clicks > 0)
            .OrderByDescending(l => l.Clicks)
            .Take(5)
            .ToList();

        return new AnalyticsSummaryDto(from, to, Totals(jobs, recipients, links), daily, templates, topLinks);
    }

    public async Task<SendAnalyticsDto> ForSendAsync(Guid sendJobId, CancellationToken ct)
    {
        var job = await db.EmailSendJobs
            .Where(j => j.Id == sendJobId && j.UserId == currentUser.UserId)
            .Select(j => new { Row = new JobRow(j.Id, j.TrackClicks, j.TemplateVersionId, null), j.UtmCampaign })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        var recipients = await LoadRecipientsAsync([job.Row.Id], ct);
        var links = await LoadLinksAsync([job.Row.Id], ct);

        var linkRows = links
            .GroupBy(l => l.TargetUrl)
            .Select(g => new SendLinkRow(g.Min(l => l.Position), DisplayUrl(g.Key), g.Sum(l => l.Human),
                g.Count(l => l.Human > 0), g.Sum(l => l.Scanner)))
            .OrderBy(l => l.Position)
            .ToList();

        var recipientRows = recipients
            .Select(r =>
            {
                var firstClick = links.Where(l => l.RecipientId == r.Id).Min(l => l.FirstHuman);
                return new SendRecipientRow(r.Id, r.Email, r.Status.ToString().ToLowerInvariant(), firstClick is not null, firstClick);
            })
            .ToList();

        return new SendAnalyticsDto(job.Row.TrackClicks, job.UtmCampaign,
            Totals([job.Row], recipients, links), linkRows, recipientRows);
    }

    private async Task<List<RecipientRow>> LoadRecipientsAsync(List<Guid> jobIds, CancellationToken ct) =>
        await db.EmailSendRecipients
            .Where(r => jobIds.Contains(r.SendJobId))
            .Select(r => new RecipientRow(r.Id, r.SendJobId, r.EmailAddress, r.Status, r.LastAttemptAt))
            .ToListAsync(ct);

    private async Task<List<LinkStat>> LoadLinksAsync(List<Guid> jobIds, CancellationToken ct) =>
        await db.TrackedLinks
            .Where(l => jobIds.Contains(l.SendJobId))
            .Select(l => new LinkStat(
                l.SendJobId, l.RecipientId, l.Position, l.TargetUrl,
                l.Clicks.Count(c => c.Classification == ClickClassifications.Human),
                l.Clicks.Count(c => c.Classification == ClickClassifications.Scanner),
                l.Clicks.Where(c => c.Classification == ClickClassifications.Human).Min(c => (DateTimeOffset?)c.OccurredAt)))
            .ToListAsync(ct);

    private static AnalyticsTotals Totals(List<JobRow> jobs, List<RecipientRow> recipients, List<LinkStat> links)
    {
        var accepted = recipients.Count(r => r.Status is RecipientStatus.Sent or RecipientStatus.Bounced);
        var bounced = recipients.Count(r => r.Status == RecipientStatus.Bounced);
        var trackedJobs = jobs.Where(j => j.TrackClicks).Select(j => j.Id).ToHashSet();
        // Click rate's base: accepted minus bounced, in tracked sends only.
        var tracked = recipients.Count(r => trackedJobs.Contains(r.SendJobId) && r.Status == RecipientStatus.Sent);
        var clickers = links.Where(l => l.Human > 0).Select(l => l.RecipientId).Distinct().Count();

        return new AnalyticsTotals(
            jobs.Count,
            recipients.Count,
            accepted,
            recipients.Count(r => r.Status == RecipientStatus.Failed),
            bounced,
            tracked,
            clickers,
            accepted > 0 ? (double)bounced / accepted : null,
            tracked > 0 ? (double)clickers / tracked : null);
    }

    /// <summary>The link as people recognize it: our own UTM tags removed.</summary>
    private static string DisplayUrl(string url)
    {
        var hash = url.IndexOf('#');
        var (main, fragment) = hash < 0 ? (url, "") : (url[..hash], url[hash..]);
        var query = main.IndexOf('?');
        if (query < 0) return url;
        var kept = main[(query + 1)..].Split('&')
            .Where(p => !p.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return main[..query] + (kept.Count > 0 ? "?" + string.Join('&', kept) : "") + fragment;
    }
}
