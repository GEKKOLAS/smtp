using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Email;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// Reading a connected mailbox and answering a message with a template, which is
/// what keeps a reply inside the original conversation.
/// </summary>
public class InboxTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";
    private const string SendScope = "openid email profile https://www.googleapis.com/auth/gmail.send";
    private const string ReadScope = SendScope + " https://www.googleapis.com/auth/gmail.readonly";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ExtractState(string url)
    {
        var query = new Uri(url).Query.TrimStart('?');
        return Uri.UnescapeDataString(
            query.Split('&').Single(p => p.StartsWith("state=")).Substring("state=".Length));
    }

    /// <summary>Registers a user, connects Gmail with the given scope, and creates a template.</summary>
    private async Task<(TestSession Session, string AccountId, string VersionId)> SetupAsync(
        string grantedScope = ReadScope)
    {
        factory.Provider.Reset();
        factory.Inbox.Reset();

        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ada" });

        var start = await session.GetAsync("/api/v1/oauth/gmail/start");
        var state = ExtractState((await start.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("authorizationUrl").GetString()!);

        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/token", new
        {
            access_token = "google-access",
            refresh_token = "google-refresh",
            expires_in = 3600,
            scope = grantedScope,
            token_type = "Bearer",
        });
        factory.OAuth.OnGet("google/userinfo",
            new { sub = $"sub-{Guid.NewGuid():N}", email = "sender@gmail.com", name = "Ada" });
        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");

        var accountId = (await (await session.GetAsync("/api/v1/email-accounts"))
                .Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("id").GetString()!;

        var create = await session.PostAsync("/api/v1/templates", new
        {
            name = $"T {Guid.NewGuid():N}",
            description = (string?)null,
            content = new
            {
                editorKind = "html",
                subject = "Marketing subject",
                preheader = (string?)null,
                mjmlSource = (string?)null,
                grapesProject = (object?)null,
                htmlBody = "<p>Our pricing starts at $10.</p>",
                textBody = (string?)null,
                variables = Array.Empty<object>(),
                assets = Array.Empty<object>(),
            },
        });
        create.EnsureSuccessStatusCode();
        var versionId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("currentVersion").GetProperty("id").GetString()!;

        return (session, accountId, versionId);
    }

    [Fact]
    public async Task Inbox_lists_messages_for_a_connected_account()
    {
        var (session, accountId, _) = await SetupAsync();
        factory.Inbox.Seed(id: "m1", subject: "Question about pricing");
        factory.Inbox.Seed(id: "m2", subject: "Invoice");

        var response = await session.GetAsync($"/api/v1/inbox?accountId={accountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal("Question about pricing", body.GetProperty("items")[0].GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Message_body_is_sanitized_and_carries_a_reply_subject()
    {
        var (session, accountId, _) = await SetupAsync();
        factory.Inbox.Seed(id: "m1", subject: "Question about pricing");

        var body = await (await session.GetAsync($"/api/v1/inbox/m1?accountId={accountId}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var html = body.GetProperty("html").GetString()!;
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("how much is it", html);
        Assert.Equal("Re: Question about pricing", body.GetProperty("replySubject").GetString());
    }

    [Fact]
    public async Task Account_without_read_scope_is_rejected_and_reported_as_not_readable()
    {
        var (session, accountId, _) = await SetupAsync(grantedScope: SendScope);

        var accounts = await (await session.GetAsync("/api/v1/email-accounts"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(accounts.GetProperty("items")[0].GetProperty("canReadInbox").GetBoolean());

        var response = await session.GetAsync($"/api/v1/inbox?accountId={accountId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("inbox.scope_missing", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Another_users_account_is_not_readable()
    {
        var (_, accountId, _) = await SetupAsync();

        var intruder = NewSession();
        await intruder.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Mallory" });

        var response = await intruder.GetAsync($"/api/v1/inbox?accountId={accountId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reply_threads_the_send_and_defaults_the_recipient_to_the_sender()
    {
        var (session, accountId, versionId) = await SetupAsync();
        factory.Inbox.Seed(
            id: "m1",
            from: "customer@example.com",
            subject: "Question about pricing",
            internetMessageId: "<parent@mail.example.com>",
            references: "<root@mail.example.com>");

        var response = await session.PostAsync("/api/v1/sends", new
        {
            connectedEmailAccountId = accountId,
            templateVersionId = versionId,
            recipients = Array.Empty<object>(),
            variables = new { },
            attachments = Array.Empty<object>(),
            scheduledAt = (string?)null,
            replyToMessageId = "m1",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(job.GetProperty("isReply").GetBoolean());
        // The thread's subject wins over the template's own.
        Assert.Equal("Re: Question about pricing", job.GetProperty("subjectSnapshot").GetString());

        Assert.True(factory.Provider.Sent.TryDequeue(out var email));
        Assert.Equal("customer@example.com", email!.To[0].Email);
        Assert.Equal("thread-m1", email.ProviderThreadId);
        Assert.Equal("<parent@mail.example.com>", email.Headers["In-Reply-To"]);
        Assert.Equal("<root@mail.example.com> <parent@mail.example.com>", email.Headers["References"]);
        Assert.Contains("Our pricing starts at", email.HtmlBody);
    }

    [Fact]
    public async Task Reply_to_an_unknown_message_is_404()
    {
        var (session, accountId, versionId) = await SetupAsync();

        var response = await session.PostAsync("/api/v1/sends", new
        {
            connectedEmailAccountId = accountId,
            templateVersionId = versionId,
            recipients = Array.Empty<object>(),
            variables = new { },
            attachments = Array.Empty<object>(),
            scheduledAt = (string?)null,
            replyToMessageId = "does-not-exist",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Provider_outage_surfaces_as_502()
    {
        var (session, accountId, _) = await SetupAsync();
        factory.Inbox.Failure = () =>
            new ProviderSendException(ProviderErrorKind.Transient, "Gmail was temporarily unavailable.");

        var response = await session.GetAsync($"/api/v1/inbox?accountId={accountId}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        factory.Inbox.Failure = null;
    }
}
