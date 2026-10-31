using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Features.Sends;

// ---- Inputs ----

public sealed record RecipientInput(
    string Email, string? Name, Guid? ContactId, IReadOnlyDictionary<string, string?>? VariableOverrides);

public sealed record SendAttachmentInput(Guid AssetId, string Disposition, string? FilenameOverride);

public sealed record CreateSendCommand(
    Guid ConnectedEmailAccountId,
    Guid TemplateVersionId,
    IReadOnlyList<RecipientInput> Recipients,
    IReadOnlyDictionary<string, string?> Variables,
    IReadOnlyList<SendAttachmentInput> Attachments,
    DateTimeOffset? ScheduledAt,
    string? IdempotencyKey,
    // Answering a message already in the mailbox: the provider message id is the
    // only reply input a client supplies; threading data is resolved server-side.
    string? ReplyToMessageId = null,
    string? SubjectOverride = null,
    // Opt-in click tracking and UTM tagging (spec 14 F4.1).
    bool TrackClicks = false,
    string? UtmCampaign = null);

public sealed record TestSendCommand(
    Guid ConnectedEmailAccountId,
    Guid TemplateVersionId,
    IReadOnlyDictionary<string, string?> Variables,
    IReadOnlyList<SendAttachmentInput> Attachments,
    string ToSelf); // "login" | "account"

// ---- Outputs ----

public sealed record RecipientCounts(int Pending, int Sending, int Sent, int Failed, int Cancelled, int Bounced);

public sealed record SendJobDto(
    Guid Id, string Status, bool IsTest, Guid AccountId, Guid TemplateVersionId,
    string SubjectSnapshot, RecipientCounts RecipientCounts,
    DateTimeOffset? ScheduledAt, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt,
    string? FailureCode, bool IsReply, string? ReplyToMessageId, bool TrackClicks, string? UtmCampaign)
{
    public static SendJobDto From(EmailSendJob job) => new(
        job.Id,
        job.Status.ToString().ToLowerInvariant(),
        job.IsTest,
        job.ConnectedEmailAccountId,
        job.TemplateVersionId,
        job.SubjectSnapshot,
        Counts(job.Recipients),
        job.ScheduledAt,
        job.CreatedAt,
        job.CompletedAt,
        job.FailureCode,
        job.IsReply,
        job.ReplyToProviderMessageId,
        job.TrackClicks,
        job.UtmCampaign);

    private static RecipientCounts Counts(IReadOnlyCollection<EmailSendRecipient> recipients) => new(
        recipients.Count(r => r.Status == RecipientStatus.Pending),
        recipients.Count(r => r.Status == RecipientStatus.Sending),
        recipients.Count(r => r.Status == RecipientStatus.Sent),
        recipients.Count(r => r.Status == RecipientStatus.Failed),
        recipients.Count(r => r.Status == RecipientStatus.Cancelled),
        recipients.Count(r => r.Status == RecipientStatus.Bounced));
}

public sealed record RecipientDto(
    Guid Id, string Email, string? DisplayName, string Status, int AttemptCount,
    string? ProviderMessageId, string? FailureCode, string? FailureMessage, DateTimeOffset? NextAttemptAt,
    DateTimeOffset? BouncedAt, string? BounceCode, string? BounceReason)
{
    public static RecipientDto From(EmailSendRecipient r) => new(
        r.Id, r.EmailAddress, r.DisplayName, r.Status.ToString().ToLowerInvariant(),
        r.AttemptCount, r.ProviderMessageId, r.FailureCode, r.FailureMessage, r.NextAttemptAt,
        r.BouncedAt, r.BounceCode, r.BounceReason);
}

public sealed record ProviderEventDto(string EventType, int? HttpStatus, string? ProviderErrorCode, DateTimeOffset CreatedAt);

public sealed record SendJobDetailDto(
    SendJobDto Job, IReadOnlyList<RecipientDto> Recipients, IReadOnlyList<ProviderEventDto> Events);

public sealed record PagedSendJobs(IReadOnlyList<SendJobDto> Items, int Page, int PageSize, int TotalCount);
