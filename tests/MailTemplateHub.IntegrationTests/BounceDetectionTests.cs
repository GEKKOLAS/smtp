using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MailTemplateHub.Infrastructure.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.IntegrationTests;

public class BounceDetectionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private static string ExtractState(string url)
    {
        var query = new Uri(url).Query.TrimStart('?');
        return Uri.UnescapeDataString(query.Split('&').Single(p => p.StartsWith("state=")).Substring("state=".Length));
    }

    /// <summary>A user with a Gmail account (inbox-readable unless told otherwise) and one sent message.</summary>
    private async Task<(TestSession Session, string JobId, string RecipientId)> SendAsync(bool canReadInbox = true)
    {
        factory.Provider.Reset();
        var session = new TestSession(factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });

        var start = await session.GetAsync("/api/v1/oauth/gmail/start");
        var state = ExtractState((await start.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("authorizationUrl").GetString()!);
        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/token", new
        {
            access_token = "google-access", refresh_token = "google-refresh", expires_in = 3600,
            scope = "openid email profile https://www.googleapis.com/auth/gmail.send"
                    + (canReadInbox ? " https://www.googleapis.com/auth/gmail.readonly" : ""),
            token_type = "Bearer",
        });
        factory.OAuth.OnGet("google/userinfo", new { sub = $"sub-{Guid.NewGuid():N}", email = "sender@gmail.com", name = "Ada" });
        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");
        var accountId = (await (await session.GetAsync("/api/v1/email-accounts")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("id").GetString()!;

        var create = await session.PostAsync("/api/v1/templates", new
        {
            name = $"T {Guid.NewGuid():N}",
            description = (string?)null,
            content = new
            {
                editorKind = "html", subject = "Hello", preheader = (string?)null, mjmlSource = (string?)null,
                grapesProject = (object?)null, htmlBody = "<p>Hello</p>", textBody = (string?)null,
                variables = Array.Empty<object>(), assets = Array.Empty<object>(),
            },
        });
        var versionId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("currentVersion").GetProperty("id").GetString()!;

        var send = await session.PostAsync("/api/v1/sends", new
        {
            connectedEmailAccountId = accountId, templateVersionId = versionId,
            recipients = new[] { new { email = "nobody@example.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null } },
        });
        var jobId = (await send.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        return (session, jobId, detail.GetProperty("recipients")[0].GetProperty("id").GetString()!);
    }

    private static byte[] Report(string recipientRef, string status = "5.1.1") => Encoding.UTF8.GetBytes($"""
        From: Mail Delivery Subsystem <mailer-daemon@googlemail.com>
        Subject: Delivery Status Notification (Failure)
        MIME-Version: 1.0
        Content-Type: multipart/report; boundary="b1"; report-type=delivery-status

        --b1
        Content-Type: text/plain

        Address not found.

        --b1
        Content-Type: message/delivery-status

        Reporting-MTA: dns; googlemail.com

        Final-Recipient: rfc822; nobody@example.com
        Action: failed
        Status: {status}
        Diagnostic-Code: smtp; 550 5.1.1 User unknown

        --b1
        Content-Type: message/rfc822

        From: sender@gmail.com
        To: nobody@example.com
        X-MailTemplateHub-Ref: {recipientRef}
        Subject: Hello

        Hello
        --b1--
        """.ReplaceLineEndings("\r\n"));

    private async Task RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DetectBouncesJob>().RunAsync(CancellationToken.None);
    }

    private static async Task<JsonElement> RecipientAsync(TestSession session, string jobId) =>
        (await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("recipients")[0];

    [Fact]
    public async Task A_delivery_report_marks_the_recipient_bounced_once()
    {
        factory.Inbox.Reset();
        var (session, jobId, recipientId) = await SendAsync();
        factory.Inbox.Notices["dsn-1"] = Report(recipientId);

        await RunJobAsync();
        await RunJobAsync(); // Already processed: nothing changes, no duplicate event.

        var recipient = await RecipientAsync(session, jobId);
        Assert.Equal("bounced", recipient.GetProperty("status").GetString());
        Assert.Equal("5.1.1", recipient.GetProperty("bounceCode").GetString());
        Assert.Equal("550 5.1.1 User unknown", recipient.GetProperty("bounceReason").GetString());

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, detail.GetProperty("job").GetProperty("recipientCounts").GetProperty("bounced").GetInt32());
        Assert.Single(detail.GetProperty("events").EnumerateArray(), e => e.GetProperty("eventType").GetString() == "bounce");
    }

    [Fact]
    public async Task Reports_about_another_accounts_mail_are_ignored()
    {
        factory.Inbox.Reset();
        // The victim's account can't read its inbox, so only the other account scans;
        // its inbox holds a forged report naming the victim's recipient.
        var (victim, victimJob, victimRecipient) = await SendAsync(canReadInbox: false);
        await SendAsync(canReadInbox: true);
        factory.Inbox.Notices["forged"] = Report(victimRecipient);

        await RunJobAsync();

        Assert.Equal("sent", (await RecipientAsync(victim, victimJob)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Temporary_failures_leave_the_recipient_sent()
    {
        factory.Inbox.Reset();
        var (session, jobId, recipientId) = await SendAsync();
        factory.Inbox.Notices["dsn-soft"] = Report(recipientId, status: "4.2.2");

        await RunJobAsync();

        Assert.Equal("sent", (await RecipientAsync(session, jobId)).GetProperty("status").GetString());
    }
}
