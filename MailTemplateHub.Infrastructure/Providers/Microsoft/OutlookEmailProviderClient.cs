using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Providers.Microsoft;

/// <summary>
/// Sends via Microsoft Graph. The same RFC 2822 message is base64-encoded and
/// posted to /me/sendMail as MIME (spec 07 §3.2), so both providers share one
/// MIME build path. Graph returns 202 with no message id.
/// </summary>
internal sealed class OutlookEmailProviderClient(
    HttpClient httpClient, IEmailMessageBuilder messageBuilder, IOptions<ProviderSendOptions> options)
    : IEmailProviderClient
{
    private readonly string _sendUrl = options.Value.GraphSendUrl;

    public EmailProvider Provider => EmailProvider.Outlook;
    public TimeSpan MinSendInterval => TimeSpan.FromSeconds(2); // conservative submission throttle

    public async Task<ProviderSendResult> SendAsync(
        ConnectedAccountContext account, OutgoingEmail email, CancellationToken ct)
    {
        using var built = messageBuilder.Build(email);
        var mimeBase64 = Convert.ToBase64String(await ReadAllAsync(built.Rfc822Stream, ct));

        using var request = new HttpRequestMessage(HttpMethod.Post, _sendUrl)
        {
            Content = new StringContent(mimeBase64, Encoding.UTF8, "text/plain"),
        };
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

        if (!response.IsSuccessStatusCode)
        {
            var code = ExtractCode(await response.Content.ReadAsStringAsync(ct));
            var kind = GraphErrorMap.Classify((int)response.StatusCode, code);
            throw new ProviderSendException(kind, GraphErrorMap.SafeMessage(kind), RetryAfter(response));
        }

        // sendMail returns 202 Accepted with no id; correlation is via the ref header.
        return new ProviderSendResult(ProviderMessageId: null, ThreadId: null, "accepted");
    }

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

    private static TimeSpan? RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter?.Delta;

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }
}
