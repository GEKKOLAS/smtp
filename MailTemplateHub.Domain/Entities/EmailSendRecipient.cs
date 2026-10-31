using System.Text.Json;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// One target of a send job. The recipient row is the unit of work: it is claimed,
/// sent, and retried independently (spec 05, 10-jobs.md J1).
/// </summary>
public sealed class EmailSendRecipient
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid SendJobId { get; init; }
    public Guid? ContactId { get; init; }
    public required string EmailAddress { get; init; }
    public string? DisplayName { get; init; }
    public JsonDocument VariableOverrides { get; init; } = JsonDocument.Parse("{}");

    public RecipientStatus Status { get; set; } = RecipientStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderThreadId { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }

    public DateTimeOffset? BouncedAt { get; set; }
    /// <summary>The DSN status, e.g. 5.1.1.</summary>
    public string? BounceCode { get; set; }
    public string? BounceReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void MarkSent(string? providerMessageId, string? threadId, DateTimeOffset now)
    {
        Status = RecipientStatus.Sent;
        ProviderMessageId = providerMessageId;
        ProviderThreadId = threadId;
        LastAttemptAt = now;
        NextAttemptAt = null;
        AttemptCount++;
        FailureCode = null;
        FailureMessage = null;
    }

    /// <summary>A hard bounce for a message the provider had accepted. Only sent mail can bounce.</summary>
    public bool MarkBounced(string code, string? reason, DateTimeOffset now)
    {
        if (Status != RecipientStatus.Sent) return false;
        Status = RecipientStatus.Bounced;
        BouncedAt = now;
        BounceCode = code.Length > 16 ? code[..16] : code;
        BounceReason = reason is { Length: > 300 } ? reason[..300] : reason;
        return true;
    }

    public void MarkFailed(string code, string? message, DateTimeOffset now)
    {
        Status = RecipientStatus.Failed;
        FailureCode = code;
        FailureMessage = message;
        LastAttemptAt = now;
        NextAttemptAt = null;
        AttemptCount++;
    }

    /// <summary>Schedule another attempt, or fail permanently once attempts are exhausted.</summary>
    public void ScheduleRetry(TimeSpan delay, int maxAttempts, string code, string? message, DateTimeOffset now)
    {
        AttemptCount++;
        LastAttemptAt = now;
        if (AttemptCount >= maxAttempts)
        {
            Status = RecipientStatus.Failed;
            FailureCode = "retries_exhausted";
            FailureMessage = message;
            NextAttemptAt = null;
        }
        else
        {
            Status = RecipientStatus.Pending;
            NextAttemptAt = now + delay;
            FailureCode = code;
            FailureMessage = message;
        }
    }
}
