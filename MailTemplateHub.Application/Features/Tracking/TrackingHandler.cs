using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Tracking;

/// <summary>Resolves tracked links and records clicks (spec 14 F4.2).</summary>
public sealed partial class TrackingHandler(IAppDbContext db, IOptions<TrackingOptions> options, IClock clock)
{
    /// <summary>
    /// The destination for a token, or null when it is unknown. Records a click
    /// unless <paramref name="record"/> is false (HEAD requests).
    /// </summary>
    public async Task<string?> FollowAsync(string token, string? userAgent, bool record, CancellationToken ct)
    {
        if (!TokenRegex().IsMatch(token)) return null;

        var link = await db.TrackedLinks
            .Include(l => l.Recipient)
            .FirstOrDefaultAsync(l => l.Token == token, ct);
        if (link is null) return null;

        if (record)
        {
            var now = clock.UtcNow;
            db.LinkClicks.Add(new LinkClick
            {
                TrackedLinkId = link.Id,
                OccurredAt = now,
                Classification = ClickClassifier.Classify(userAgent, link.Recipient?.LastAttemptAt, now,
                    TimeSpan.FromSeconds(options.Value.ScannerWindowSeconds)),
            });
            await db.SaveChangesAsync(ct);
        }
        return link.TargetUrl;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{22}$")]
    private static partial Regex TokenRegex();
}
