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
}

public enum SendAttachmentDisposition
{
    Attachment,
    Inline,
}
