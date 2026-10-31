using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Providers.Microsoft;

/// <summary>
/// Reads the Outlook inbox via Microsoft Graph. Unlike Gmail, Graph returns the
/// full row set in one call (no per-message hydration), and paging is a whole
/// @odata.nextLink URL that we hand back as the page token.
/// </summary>
internal sealed class OutlookInboxClient(HttpClient httpClient, IOptions<ProviderSendOptions> options)
    : IInboxProviderClient
{
    private readonly string _inboxUrl = options.Value.GraphInboxUrl;
    private readonly string _messagesUrl = options.Value.GraphMessagesUrl;

    private const string SummaryFields =
        "id,conversationId,subject,from,receivedDateTime,bodyPreview,isRead,internetMessageId";

    public EmailProvider Provider => EmailProvider.Outlook;

    public async Task<InboxPage> ListInboxAsync(
        ConnectedAccountContext account, string? pageToken, string? search, int pageSize, CancellationToken ct)
    {
        // A page token is already a complete Graph URL (@odata.nextLink).
        var url = pageToken;
        if (string.IsNullOrEmpty(url))
        {
            url = $"{_inboxUrl}?$top={pageSize}&$select={SummaryFields}&$orderby=receivedDateTime desc";
            if (!string.IsNullOrWhiteSpace(search))
            {
                // $search and $orderby are mutually exclusive in Graph.
                url = $"{_inboxUrl}?$top={pageSize}&$select={SummaryFields}"
                      + $"&$search={Uri.EscapeDataString($"\"{search}\"")}";
            }
        }

        using var doc = await GetJsonAsync(url, account.AccessToken, ct);
        var root = doc.RootElement;

        var items = new List<InboxMessageSummary>();
        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in value.EnumerateArray())
            {
                var (fromEmail, fromName) = ReadFrom(message);
                items.Add(new InboxMessageSummary(
                    message.GetProperty("id").GetString()!,
                    StringOrNull(message, "conversationId"),
                    fromEmail,
                    fromName,
                    StringOrNull(message, "subject") ?? "(no subject)",
                    ReadDate(message, "receivedDateTime"),
                    StringOrNull(message, "bodyPreview") ?? "",
                    message.TryGetProperty("isRead", out var read)
                    && read.ValueKind is JsonValueKind.True or JsonValueKind.False && read.GetBoolean()));
            }
        }

        var next = StringOrNull(root, "@odata.nextLink");
        return new InboxPage(items, next);
    }

    public async Task<IReadOnlyList<string>> FindDeliveryNotificationsAsync(
        ConnectedAccountContext account, int max, CancellationToken ct)
    {
        // Exchange titles its reports "Undeliverable:"; other servers send from postmaster or mailer-daemon.
        var page = await ListInboxAsync(
            account, null, "subject:Undeliverable OR from:postmaster OR from:mailer-daemon", max, ct);
        return page.Items.Select(i => i.Id).ToList();
    }

    public async Task<byte[]> GetRawMessageAsync(ConnectedAccountContext account, string messageId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_messagesUrl}/{Uri.EscapeDataString(messageId)}/$value");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderSendException(ProviderErrorKind.Transient, "Outlook was unreachable.", inner: ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode) return await response.Content.ReadAsByteArrayAsync(ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ProviderSendException(ProviderErrorKind.PermanentOther, "The message was not found.");
            }
            var kind = GraphErrorMap.Classify((int)response.StatusCode, ExtractCode(await response.Content.ReadAsStringAsync(ct)));
            throw new ProviderSendException(kind, InboxSafeMessage(kind), response.Headers.RetryAfter?.Delta);
        }
    }

    public async Task<InboxMessageDetail> GetMessageAsync(
        ConnectedAccountContext account, string messageId, CancellationToken ct)
    {
        var url = $"{_messagesUrl}/{Uri.EscapeDataString(messageId)}"
                  + "?$select=id,conversationId,subject,from,toRecipients,receivedDateTime,body,"
                  + "internetMessageId,internetMessageHeaders";

        using var doc = await GetJsonAsync(url, account.AccessToken, ct);
        var root = doc.RootElement;

        var (fromEmail, fromName) = ReadFrom(root);
        var (html, text) = ReadBody(root);

        return new InboxMessageDetail(
            root.GetProperty("id").GetString()!,
            StringOrNull(root, "conversationId"),
            fromEmail,
            fromName,
            ReadRecipients(root, "toRecipients"),
            StringOrNull(root, "subject") ?? "(no subject)",
            ReadDate(root, "receivedDateTime"),
            html,
            text,
            StringOrNull(root, "internetMessageId"),
            ReadHeader(root, "References"));
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Required for $search on mail; harmless otherwise.
        request.Headers.Add("ConsistencyLevel", "eventual");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderSendException(ProviderErrorKind.Transient, "Outlook was unreachable.", inner: ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ProviderSendException(ProviderErrorKind.PermanentOther, "The message was not found.");
            }
            var kind = GraphErrorMap.Classify((int)response.StatusCode, ExtractCode(body));
            throw new ProviderSendException(kind, InboxSafeMessage(kind), response.Headers.RetryAfter?.Delta);
        }

        return JsonDocument.Parse(body);
    }

    private static string InboxSafeMessage(ProviderErrorKind kind) => kind switch
    {
        ProviderErrorKind.AuthExpired => "The Outlook access token expired.",
        ProviderErrorKind.AuthRevoked => "Outlook access was revoked; reconnect the account.",
        ProviderErrorKind.InsufficientScope => "The Outlook connection lacks inbox read permission.",
        ProviderErrorKind.Transient => "Outlook was temporarily unavailable.",
        _ => "Outlook could not read the mailbox.",
    };

    private static string? ExtractCode(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("code", out var code))
            {
                return code.GetString();
            }
        }
        catch (JsonException) { /* fall through */ }
        return null;
    }

    private static (string Email, string? Name) ReadFrom(JsonElement message)
    {
        if (message.TryGetProperty("from", out var from)
            && from.ValueKind == JsonValueKind.Object
            && from.TryGetProperty("emailAddress", out var address))
        {
            var name = StringOrNull(address, "name");
            return (StringOrNull(address, "address") ?? "", string.IsNullOrWhiteSpace(name) ? null : name);
        }
        return ("", null);
    }

    private static IReadOnlyList<string> ReadRecipients(JsonElement message, string property)
    {
        if (!message.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array) return [];
        return
        [
            .. list.EnumerateArray()
                .Select(r => r.TryGetProperty("emailAddress", out var a) ? StringOrNull(a, "address") : null)
                .Where(a => !string.IsNullOrEmpty(a))
                .Cast<string>(),
        ];
    }

    private static (string? Html, string? Text) ReadBody(JsonElement message)
    {
        if (!message.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }
        var content = StringOrNull(body, "content");
        var type = StringOrNull(body, "contentType");
        return string.Equals(type, "html", StringComparison.OrdinalIgnoreCase)
            ? (content, null)
            : (null, content);
    }

    /// <summary>Reads one RFC 5322 header out of the internetMessageHeaders collection.</summary>
    private static string? ReadHeader(JsonElement message, string name)
    {
        if (!message.TryGetProperty("internetMessageHeaders", out var headers)
            || headers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var header in headers.EnumerateArray())
        {
            if (string.Equals(StringOrNull(header, "name"), name, StringComparison.OrdinalIgnoreCase))
            {
                return StringOrNull(header, "value");
            }
        }
        return null;
    }

    private static DateTimeOffset? ReadDate(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static string? StringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
