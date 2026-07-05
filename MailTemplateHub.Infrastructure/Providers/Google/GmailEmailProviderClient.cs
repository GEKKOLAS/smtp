using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Providers.Google;

/// <summary>
/// Sends via the Gmail API (users.messages.send) with a raw RFC 2822 message
/// (spec 07 §3.1). One MIME path for both providers.
/// </summary>
internal sealed class GmailEmailProviderClient(HttpClient httpClient, IEmailMessageBuilder messageBuilder)
    : IEmailProviderClient
{
    private const string SendUrl = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

    public EmailProvider Provider => EmailProvider.Gmail;
    public TimeSpan MinSendInterval => TimeSpan.FromSeconds(1); // ~1 send/sec/account

    public async Task<ProviderSendResult> SendAsync(
        ConnectedAccountContext account, OutgoingEmail email, CancellationToken ct)
    {
        using var built = messageBuilder.Build(email);
        var raw = Base64Url(await ReadAllAsync(built.Rfc822Stream, ct));

        using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl)
        {
            Content = JsonContent.Create(new { raw }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

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
            var reason = ExtractReason(body);
            var kind = GoogleErrorMap.Classify((int)response.StatusCode, reason);
            throw new ProviderSendException(kind, GoogleErrorMap.SafeMessage(kind), RetryAfter(response));
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new ProviderSendResult(
            root.TryGetProperty("id", out var id) ? id.GetString() : null,
            root.TryGetProperty("threadId", out var thread) ? thread.GetString() : null,
            "sent");
    }

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

    private static TimeSpan? RetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter?.Delta;

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
