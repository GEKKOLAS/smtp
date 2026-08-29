using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// In-process inbox double: serves a scripted mailbox so tests can read messages
/// and reply to them without a provider. Mirrors <see cref="FakeEmailProviderClient"/>.
/// </summary>
public sealed class FakeInboxClient : IInboxProviderClient
{
    public EmailProvider Provider { get; set; } = EmailProvider.Gmail;

    public List<InboxMessageDetail> Messages { get; } = [];
    public string? NextPageToken { get; set; }

    /// <summary>Set to throw instead of answering, to exercise provider-failure mapping.</summary>
    public Func<ProviderSendException>? Failure { get; set; }

    public Task<InboxPage> ListInboxAsync(
        ConnectedAccountContext account, string? pageToken, string? search, int pageSize, CancellationToken ct)
    {
        if (Failure is not null) throw Failure();

        var items = Messages
            .Where(m => string.IsNullOrEmpty(search)
                        || m.Subject.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(pageSize)
            .Select(m => new InboxMessageSummary(
                m.Id, m.ThreadId, m.FromEmail, m.FromName, m.Subject, m.ReceivedAt,
                m.TextBody ?? "", IsRead: true))
            .ToList();

        return Task.FromResult(new InboxPage(items, NextPageToken));
    }

    public Task<InboxMessageDetail> GetMessageAsync(
        ConnectedAccountContext account, string messageId, CancellationToken ct)
    {
        if (Failure is not null) throw Failure();

        var message = Messages.FirstOrDefault(m => m.Id == messageId)
            ?? throw new ProviderSendException(ProviderErrorKind.PermanentOther, "The message was not found.");
        return Task.FromResult(message);
    }

    public void Reset()
    {
        Messages.Clear();
        NextPageToken = null;
        Failure = null;
    }

    /// <summary>Adds a message with the threading identifiers a reply needs.</summary>
    public InboxMessageDetail Seed(
        string id = "msg-1",
        string from = "customer@example.com",
        string subject = "Question about pricing",
        string? internetMessageId = "<parent@mail.example.com>",
        string? references = null)
    {
        var message = new InboxMessageDetail(
            id,
            ThreadId: $"thread-{id}",
            FromEmail: from,
            FromName: "A Customer",
            To: ["sender@gmail.com"],
            Subject: subject,
            ReceivedAt: DateTimeOffset.UtcNow,
            HtmlBody: "<p>Hi, how much is it?<script>alert(1)</script></p>",
            TextBody: "Hi, how much is it?",
            InternetMessageId: internetMessageId,
            References: references);
        Messages.Add(message);
        return message;
    }
}

public sealed class FakeInboxClientFactory(FakeInboxClient client) : IInboxProviderClientFactory
{
    public IInboxProviderClient For(EmailProvider provider) => client;
}
