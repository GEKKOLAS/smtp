using System.Text.Json;
using MailTemplateHub.Domain.Common;
using MailTemplateHub.Domain.Enums;
using MailTemplateHub.Domain.Errors;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// One send operation (1..n recipients) with the lifecycle
/// Scheduled → Queued → Sending → Sent/PartiallyFailed/Failed, plus Retrying and
/// Cancelled (spec 05, 07 §4). The state machine lives here.
/// </summary>
public sealed class EmailSendJob : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid ConnectedEmailAccountId { get; init; }
    public required Guid TemplateVersionId { get; init; }

    public SendJobStatus Status { get; set; } = SendJobStatus.Queued;
    public bool IsTest { get; init; }
    public required string SubjectSnapshot { get; set; }
    public JsonDocument VariableValues { get; init; } = JsonDocument.Parse("{}");

    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? RenderedSnapshotKey { get; set; }
    public long? TotalSizeBytes { get; set; }
    public string? IdempotencyKey { get; init; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ConnectedEmailAccount? Account { get; init; }
    public EmailTemplateVersion? TemplateVersion { get; init; }
    public List<EmailSendRecipient> Recipients { get; init; } = [];
    public List<EmailSendAttachment> Attachments { get; init; } = [];

    public void MarkSending(DateTimeOffset now)
    {
        if (Status is not (SendJobStatus.Queued or SendJobStatus.Retrying))
            throw new DomainException(ErrorCodes.Send.InvalidTransition,
                $"Cannot start a job in status {Status}.");
        Status = SendJobStatus.Sending;
        StartedAt ??= now;
        AttemptCount++;
        NextAttemptAt = null;
    }

    /// <summary>Finalize from recipient outcomes, or park for a job-level retry.</summary>
    public void Finalize(DateTimeOffset now)
    {
        var sent = Recipients.Count(r => r.Status == RecipientStatus.Sent);
        var failed = Recipients.Count(r => r.Status == RecipientStatus.Failed);
        var cancelled = Recipients.Count(r => r.Status == RecipientStatus.Cancelled);
        var pendingWithFuture = Recipients.Any(r =>
            r.Status is RecipientStatus.Pending or RecipientStatus.Sending);

        if (pendingWithFuture)
        {
            Status = SendJobStatus.Retrying;
            NextAttemptAt = Recipients
                .Where(r => r.NextAttemptAt is not null)
                .Min(r => r.NextAttemptAt);
            return;
        }

        Status = (sent, failed) switch
        {
            (> 0, 0) => SendJobStatus.Sent,
            (> 0, > 0) => SendJobStatus.PartiallyFailed,
            (0, 0) when cancelled > 0 => SendJobStatus.Cancelled,
            _ => SendJobStatus.Failed,
        };
        CompletedAt = now;
        NextAttemptAt = null;
    }

    public void FailAll(string code, string? message, DateTimeOffset now)
    {
        foreach (var recipient in Recipients.Where(r =>
                     r.Status is RecipientStatus.Pending or RecipientStatus.Sending))
        {
            recipient.MarkFailed(code, message, now);
        }
        FailureCode = code;
        FailureMessage = message;
        Finalize(now);
    }

    public bool CanCancel => Status is SendJobStatus.Scheduled or SendJobStatus.Queued
        or SendJobStatus.Sending or SendJobStatus.Retrying;

    public void Cancel(DateTimeOffset now)
    {
        foreach (var recipient in Recipients.Where(r => r.Status == RecipientStatus.Pending))
        {
            recipient.Status = RecipientStatus.Cancelled;
        }
        Status = SendJobStatus.Cancelled;
        CompletedAt = now;
        NextAttemptAt = null;
    }
}
