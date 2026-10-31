using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Abstractions.Email;

/// <summary>One inbox message as shown in the list view. Bodies are not fetched here.</summary>
public sealed record InboxMessageSummary(
    string Id,
    string? ThreadId,
    string FromEmail,
    string? FromName,
    string Subject,
    DateTimeOffset? ReceivedAt,
    string Snippet,
    bool IsRead);

public sealed record InboxPage(IReadOnlyList<InboxMessageSummary> Items, string? NextPageToken);

/// <summary>
/// A single message with body and the RFC 5322 identifiers needed to reply in
/// the same thread (In-Reply-To/References, plus the provider thread id).
/// </summary>
public sealed record InboxMessageDetail(
    string Id,
    string? ThreadId,
    string FromEmail,
    string? FromName,
    IReadOnlyList<string> To,
    string Subject,
    DateTimeOffset? ReceivedAt,
    string? HtmlBody,
    string? TextBody,
    string? InternetMessageId,
    string? References);

/// <summary>
/// Reads a connected account's inbox via the provider API. Like
/// <see cref="IEmailProviderClient"/>, no provider SDK type leaks past this port;
/// failures surface as <see cref="ProviderSendException"/> with a typed kind.
/// </summary>
public interface IInboxProviderClient
{
    EmailProvider Provider { get; }

    Task<InboxPage> ListInboxAsync(
        ConnectedAccountContext account, string? pageToken, string? search, int pageSize, CancellationToken ct);

    Task<InboxMessageDetail> GetMessageAsync(
        ConnectedAccountContext account, string messageId, CancellationToken ct);

    /// <summary>
    /// Ids of recent delivery-status notifications (bounces) in the inbox. The
    /// search differs per provider, so it lives here rather than in the caller.
    /// </summary>
    Task<IReadOnlyList<string>> FindDeliveryNotificationsAsync(
        ConnectedAccountContext account, int max, CancellationToken ct);

    /// <summary>The whole message as RFC 5322 bytes, for parsing MIME reports.</summary>
    Task<byte[]> GetRawMessageAsync(ConnectedAccountContext account, string messageId, CancellationToken ct);
}

public interface IInboxProviderClientFactory
{
    IInboxProviderClient For(EmailProvider provider);
}
