using System.Net;
using System.Text;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using MailTemplateHub.Infrastructure.Providers.Google;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.UnitTests.Providers;

/// <summary>
/// Parsing of the Gmail message payload: the pieces a reply depends on are the
/// Message-ID/References headers and the base64url-encoded MIME part tree.
/// </summary>
public class GmailInboxClientTests
{
    private static readonly ConnectedAccountContext Account =
        new(Guid.NewGuid(), EmailProvider.Gmail, "me@gmail.com", "token");

    private static GmailInboxClient Client(RoutingHandler handler) =>
        new(new HttpClient(handler),
            Options.Create(new ProviderSendOptions { GmailMessagesUrl = "https://gmail.test/messages" }));

    private static GmailInboxClient Client(params (string UrlFragment, string Body)[] responses) =>
        Client(new RoutingHandler(responses));

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public async Task Reads_headers_and_walks_nested_parts_for_bodies()
    {
        var json = $$"""
        {
          "id": "m1",
          "threadId": "t1",
          "internalDate": "1750000000000",
          "payload": {
            "mimeType": "multipart/mixed",
            "headers": [
              { "name": "From", "value": "A Customer <customer@example.com>" },
              { "name": "To", "value": "me@gmail.com" },
              { "name": "Subject", "value": "Question about pricing" },
              { "name": "Message-ID", "value": "<parent@mail.example.com>" },
              { "name": "References", "value": "<root@mail.example.com>" }
            ],
            "parts": [
              {
                "mimeType": "multipart/alternative",
                "parts": [
                  { "mimeType": "text/plain", "body": { "data": "{{Base64Url("plain body")}}" } },
                  { "mimeType": "text/html", "body": { "data": "{{Base64Url("<p>html body</p>")}}" } }
                ]
              }
            ]
          }
        }
        """;

        var message = await Client(("messages/m1", json)).GetMessageAsync(Account, "m1", CancellationToken.None);

        Assert.Equal("customer@example.com", message.FromEmail);
        Assert.Equal("A Customer", message.FromName);
        Assert.Equal("Question about pricing", message.Subject);
        Assert.Equal("<parent@mail.example.com>", message.InternetMessageId);
        Assert.Equal("<root@mail.example.com>", message.References);
        Assert.Equal("<p>html body</p>", message.HtmlBody);
        Assert.Equal("plain body", message.TextBody);
        Assert.Equal("t1", message.ThreadId);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1750000000000), message.ReceivedAt);
    }

    [Fact]
    public async Task Lists_inbox_ids_and_hydrates_each_summary()
    {
        const string list = """
        { "messages": [ { "id": "m1" } ], "nextPageToken": "page-2" }
        """;
        const string metadata = """
        {
          "id": "m1",
          "threadId": "t1",
          "snippet": "Hi &amp; hello",
          "labelIds": ["INBOX", "UNREAD"],
          "payload": { "headers": [ { "name": "From", "value": "customer@example.com" },
                                    { "name": "Subject", "value": "Hi" } ] }
        }
        """;

        var page = await Client(("messages/m1", metadata), ("messages", list))
            .ListInboxAsync(Account, pageToken: null, search: null, pageSize: 25, CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal("m1", item.Id);
        Assert.Equal("customer@example.com", item.FromEmail);
        Assert.Equal("Hi & hello", item.Snippet);
        Assert.False(item.IsRead);
        Assert.Equal("page-2", page.NextPageToken);
    }

    [Fact]
    public async Task Insufficient_scope_maps_to_a_typed_provider_error()
    {
        const string error = """
        { "error": { "code": 403, "errors": [ { "reason": "insufficientPermissions" } ] } }
        """;
        var handler = new RoutingHandler([("messages", error)]) { Status = HttpStatusCode.Forbidden };

        var exception = await Assert.ThrowsAsync<ProviderSendException>(() =>
            Client(handler).ListInboxAsync(Account, null, null, 25, CancellationToken.None));

        Assert.Equal(ProviderErrorKind.InsufficientScope, exception.Kind);
    }

    /// <summary>Answers each request with the first response whose fragment matches the URL.</summary>
    private sealed class RoutingHandler((string UrlFragment, string Body)[] responses) : HttpMessageHandler
    {
        private readonly (string UrlFragment, string Body)[] _responses = responses;

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            var body = _responses.First(r => url.Contains(r.UrlFragment, StringComparison.Ordinal)).Body;
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
