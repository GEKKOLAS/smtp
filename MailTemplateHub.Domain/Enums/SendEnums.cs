namespace MailTemplateHub.Domain.Enums;

public enum SendJobStatus
{
    Scheduled,
    Queued,
    Sending,
    Sent,
    PartiallyFailed,
    Failed,
    Retrying,
    Cancelled,
}

public enum RecipientStatus
{
    Pending,
    Sending,
    Sent,
    Failed,
    Cancelled,

    /// <summary>Accepted by the provider, then returned by the receiving server (spec 14 F4.3).</summary>
    Bounced,
}

public enum SendAttachmentDisposition
{
    Attachment,
    Inline,
}
