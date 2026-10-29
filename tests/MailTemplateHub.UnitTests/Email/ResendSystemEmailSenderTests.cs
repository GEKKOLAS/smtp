using System.Net;
using System.Text;
using System.Text.Json;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.UnitTests.Email;

public class ResendSystemEmailSenderTests
{
    private static (ResendSystemEmailSender Sender, CapturingHandler Handler) Build(
        HttpStatusCode status = HttpStatusCode.OK, string body = """{"id":"re_123"}""")
    {
        var handler = new CapturingHandler(status, body);
        var sender = new ResendSystemEmailSender(
            new HttpClient(handler),
            Options.Create(new EmailSenderOptions
            {
                ApiKey = "re_test_key",
                FromAddress = "noreply@example.com",
                FromName = "Mail Template Hub",
            }),
            Options.Create(new OAuthGeneralOptions { FrontendBaseUrl = "https://app.example.com/" }),
            NullLogger<ResendSystemEmailSender>.Instance);
        return (sender, handler);
    }

    [Fact]
    public async Task Posts_a_reset_link_built_from_the_frontend_base_url()
    {
        var (sender, handler) = Build();

        await sender.SendPasswordResetAsync("ada@example.com", "tok en/+value", CancellationToken.None);

        var payload = JsonDocument.Parse(handler.Body!).RootElement;
        Assert.Equal("Mail Template Hub <noreply@example.com>", payload.GetProperty("from").GetString());
        Assert.Equal("ada@example.com", payload.GetProperty("to")[0].GetString());

        // The token is URL-escaped exactly once, and the base URL's trailing slash is not doubled.
        var expected = "https://app.example.com/reset-password?token=tok%20en%2F%2Bvalue";
        Assert.Contains(expected, payload.GetProperty("html").GetString());
        Assert.Contains(expected, payload.GetProperty("text").GetString());
        Assert.Equal("Bearer re_test_key", handler.Authorization);
    }

    [Fact]
    public async Task Provider_rejection_is_swallowed_so_the_endpoint_stays_202()
    {
        var (sender, _) = Build(HttpStatusCode.UnprocessableEntity, """{"message":"domain not verified"}""");

        // Must not throw: the caller answers 202 regardless of whether the address exists.
        await sender.SendPasswordResetAsync("ada@example.com", "token", CancellationToken.None);
    }

    [Fact]
    public async Task Network_failure_is_swallowed()
    {
        var sender = new ResendSystemEmailSender(
            new HttpClient(new ThrowingHandler()),
            Options.Create(new EmailSenderOptions { ApiKey = "k", FromAddress = "a@b.com" }),
            Options.Create(new OAuthGeneralOptions { FrontendBaseUrl = "https://app.example.com" }),
            NullLogger<ResendSystemEmailSender>.Instance);

        await sender.SendPasswordResetAsync("ada@example.com", "token", CancellationToken.None);
    }

    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }
}
