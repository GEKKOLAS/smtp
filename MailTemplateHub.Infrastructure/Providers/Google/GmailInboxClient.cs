using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Providers.Google;

/// <summary>
/// Reads the Gmail inbox (users.messages.list/get). The list call returns only
/// ids, so summaries are hydrated with per-message metadata fetches (bounded
/// concurrency). Bodies come from a format=full get, walking the MIME part tree.
/// </summary>
internal sealed class GmailInboxClient(HttpClient httpClient, IOptions<ProviderSendOptions> options)
    : IInboxProviderClient
{
    private readonly string _messagesUrl = options.Value.GmailMessagesUrl;
    private const int MetadataFetchConcurrency = 6;

    public EmailProvider Provider => EmailProvider.Gmail;

    public async Task<InboxPage> ListInboxAsync(
        ConnectedAccountContext account, string? pageToken, string? search, int pageSize, CancellationToken ct)
    {
        var url = $"{_messagesUrl}?labelIds=INBOX&maxResults={pageSize}";
        if (!string.IsNullOrEmpty(pageToken)) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&q={Uri.EscapeDataString(search)}";

        using var doc = await GetJsonAsync(url, account.AccessToken, ct);
        var root = doc.RootElement;
        var ids = ReadIds(root);

        var summaries = new InboxMessageSummary?[ids.Count];
        using var throttle = new SemaphoreSlim(MetadataFetchConcurrency);
        await Task.WhenAll(ids.Select(async (id, index) =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                summaries[index] = await FetchSummaryAsync(id, account.AccessToken, ct);
            }
            catch (ProviderSendException ex) when (ex.Kind == ProviderErrorKind.PermanentOther)
            {
                // Deleted or moved between the list and this fetch — drop the row
                // rather than failing the whole page. Auth and quota errors still
                // propagate, so the caller can prompt a reconnect.
                summaries[index] = null;
            }
            finally
            {
                throttle.Release();
            }
        }));

        var nextToken = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        return new InboxPage([.. summaries.Where(s => s is not null).Cast<InboxMessageSummary>()], nextToken);
    }

    public async Task<IReadOnlyList<string>> FindDeliveryNotificationsAsync(
        ConnectedAccountContext account, int max, CancellationToken ct)
    {
        // Gmail's own bounces come from mailer-daemon; other servers' from postmaster.
        var query = Uri.EscapeDataString("from:(mailer-daemon OR postmaster) newer_than:3d");
        using var doc = await GetJsonAsync($"{_messagesUrl}?labelIds=INBOX&maxResults={max}&q={query}",
            account.AccessToken, ct);
        return ReadIds(doc.RootElement);
    }

    public async Task<byte[]> GetRawMessageAsync(ConnectedAccountContext account, string messageId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"{_messagesUrl}/{Uri.EscapeDataString(messageId)}?format=raw",
            account.AccessToken, ct);
        if (!doc.RootElement.TryGetProperty("raw", out var raw) || raw.GetString() is not { } encoded)
        {
            throw new ProviderSendException(ProviderErrorKind.PermanentOther, "The message was not found.");
        }
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }

    private static List<string> ReadIds(JsonElement root)
    {
        var ids = new List<string>();
        if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (message.TryGetProperty("id", out var id) && id.GetString() is { } value) ids.Add(value);
            }
        }
        return ids;
    }

    public async Task<InboxMessageDetail> GetMessageAsync(
        ConnectedAccountContext account, string messageId, CancellationToken ct)
    {
        var url = $"{_messagesUrl}/{Uri.EscapeDataString(messageId)}?format=full";
        using var doc = await GetJsonAsync(url, account.AccessToken, ct);
        var root = doc.RootElement;

        var headers = ReadHeaders(root);
        var (fromEmail, fromName) = ParseMailbox(headers.GetValueOrDefault("from"));
        var (html, text) = ExtractBodies(root);

        return new InboxMessageDetail(
            root.GetProperty("id").GetString()!,
            root.TryGetProperty("threadId", out var thread) ? thread.GetString() : null,
            fromEmail,
            fromName,
            ParseAddressList(headers.GetValueOrDefault("to")),
            headers.GetValueOrDefault("subject") ?? "(no subject)",
            ReadInternalDate(root),
            html,
            text,
            headers.GetValueOrDefault("message-id"),
            headers.GetValueOrDefault("references"));
    }

    private async Task<InboxMessageSummary?> FetchSummaryAsync(string id, string accessToken, CancellationToken ct)
    {
        var url = $"{_messagesUrl}/{Uri.EscapeDataString(id)}"
                  + "?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date";
        using var doc = await GetJsonAsync(url, accessToken, ct);
        var root = doc.RootElement;

        var headers = ReadHeaders(root);
        var (fromEmail, fromName) = ParseMailbox(headers.GetValueOrDefault("from"));
        var unread = root.TryGetProperty("labelIds", out var labels)
                     && labels.ValueKind == JsonValueKind.Array
                     && labels.EnumerateArray().Any(l => l.GetString() == "UNREAD");

        return new InboxMessageSummary(
            root.GetProperty("id").GetString()!,
            root.TryGetProperty("threadId", out var thread) ? thread.GetString() : null,
            fromEmail,
            fromName,
            headers.GetValueOrDefault("subject") ?? "(no subject)",
            ReadInternalDate(root),
            WebUtility.HtmlDecode(root.TryGetProperty("snippet", out var snippet) ? snippet.GetString() ?? "" : ""),
            !unread);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderSendException(ProviderErrorKind.Transient, "Gmail was unreachable.", inner: ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ProviderSendException(ProviderErrorKind.PermanentOther, "The message was not found.");
            }
            var kind = GoogleErrorMap.Classify((int)response.StatusCode, ExtractReason(body));
            throw new ProviderSendException(kind, InboxSafeMessage(kind), response.Headers.RetryAfter?.Delta);
        }

        return JsonDocument.Parse(body);
    }

    private static string InboxSafeMessage(ProviderErrorKind kind) => kind switch
    {
        ProviderErrorKind.AuthExpired => "The Gmail access token expired.",
        ProviderErrorKind.AuthRevoked => "Gmail access was revoked; reconnect the account.",
        ProviderErrorKind.InsufficientScope => "The Gmail connection lacks inbox read permission.",
        ProviderErrorKind.Transient => "Gmail was temporarily unavailable.",
        _ => "Gmail could not read the mailbox.",
    };

    private static string? ExtractReason(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                    && errors[0].TryGetProperty("reason", out var reason))
                {
                    return reason.GetString();
                }
                if (error.TryGetProperty("status", out var status)) return status.GetString();
            }
        }
        catch (JsonException) { /* fall through */ }
        return null;
    }

    /// <summary>Header names → values from payload.headers, lower-cased names.</summary>
    private static Dictionary<string, string> ReadHeaders(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("payload", out var payload)
            || !payload.TryGetProperty("headers", out var headers)
            || headers.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var header in headers.EnumerateArray())
        {
            var name = header.TryGetProperty("name", out var n) ? n.GetString() : null;
            var value = header.TryGetProperty("value", out var v) ? v.GetString() : null;
            if (name is not null && value is not null) result[name.ToLowerInvariant()] = value;
        }
        return result;
    }

    private static DateTimeOffset? ReadInternalDate(JsonElement root) =>
        root.TryGetProperty("internalDate", out var value)
        && long.TryParse(value.GetString(), out var millis)
            ? DateTimeOffset.FromUnixTimeMilliseconds(millis)
            : null;

    private static (string Email, string? Name) ParseMailbox(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return ("", null);
        if (MimeKit.MailboxAddress.TryParse(header, out var mailbox))
        {
            return (mailbox.Address, string.IsNullOrWhiteSpace(mailbox.Name) ? null : mailbox.Name);
        }
        return (header.Trim(), null);
    }

    private static IReadOnlyList<string> ParseAddressList(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return [];
        if (MimeKit.InternetAddressList.TryParse(header, out var list))
        {
            return [.. list.Mailboxes.Select(m => m.Address)];
        }
        return [header.Trim()];
    }

    /// <summary>Depth-first walk of the payload part tree for text/html and text/plain bodies.</summary>
    private static (string? Html, string? Text) ExtractBodies(JsonElement root)
    {
        string? html = null, text = null;
        if (root.TryGetProperty("payload", out var payload)) Walk(payload, ref html, ref text);
        return (html, text);

        static void Walk(JsonElement part, ref string? html, ref string? text)
        {
            var mimeType = part.TryGetProperty("mimeType", out var mt) ? mt.GetString() : null;
            if (mimeType is "text/html" or "text/plain"
                && part.TryGetProperty("body", out var body)
                && body.TryGetProperty("data", out var data)
                && data.GetString() is { Length: > 0 } encoded)
            {
                var decoded = DecodeBase64Url(encoded);
                if (mimeType == "text/html") html ??= decoded;
                else text ??= decoded;
            }

            if (part.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in parts.EnumerateArray()) Walk(child, ref html, ref text);
            }
        }
    }

    private static string DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException)
        {
            return "";
        }
    }
}
