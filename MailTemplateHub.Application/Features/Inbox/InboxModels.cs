using MailTemplateHub.Application.Abstractions.Email;

namespace MailTemplateHub.Application.Features.Inbox;

public sealed record InboxListQuery(Guid AccountId, string? PageToken, string? Search, int PageSize);

public sealed record InboxMessageDto(
    string Id,
    string? ThreadId,
    string FromEmail,
    string? FromName,
    string Subject,
    DateTimeOffset? ReceivedAt,
    string Snippet,
    bool IsRead)
{
    public static InboxMessageDto From(InboxMessageSummary m) => new(
        m.Id, m.ThreadId, m.FromEmail, m.FromName, m.Subject, m.ReceivedAt, m.Snippet, m.IsRead);
}

public sealed record InboxPageDto(IReadOnlyList<InboxMessageDto> Items, string? NextPageToken);

/// <summary>
/// A single message for the reader pane. The body is sanitized before it leaves
/// the server — it is attacker-controlled HTML from an arbitrary sender.
/// </summary>
public sealed record InboxMessageDetailDto(
    string Id,
    string? ThreadId,
    string FromEmail,
    string? FromName,
    IReadOnlyList<string> To,
    string Subject,
    DateTimeOffset? ReceivedAt,
    string? Html,
    string? Text,
    // Subject pre-formatted for a reply ("Re: …"), ready for the compose form.
    string ReplySubject);
