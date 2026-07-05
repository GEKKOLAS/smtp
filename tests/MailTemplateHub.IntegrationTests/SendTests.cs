using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Email;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class SendTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ExtractState(string url)
    {
        var query = new Uri(url).Query.TrimStart('?');
        return Uri.UnescapeDataString(query.Split('&').Single(p => p.StartsWith("state=")).Substring("state=".Length));
    }

    /// <summary>Registers a user, connects a Gmail account, and creates a template.</summary>
    private async Task<(TestSession Session, string AccountId, string VersionId)> SetupAsync(
        object? content = null)
    {
        factory.Provider.Reset();
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ada" });

        var start = await session.GetAsync("/api/v1/oauth/gmail/start");
        var state = ExtractState((await start.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("authorizationUrl").GetString()!);
        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/token", new
        {
            access_token = "google-access", refresh_token = "google-refresh", expires_in = 3600,
            scope = "openid email profile https://www.googleapis.com/auth/gmail.send", token_type = "Bearer",
        });
        factory.OAuth.OnGet("google/userinfo", new { sub = $"sub-{Guid.NewGuid():N}", email = "sender@gmail.com", name = "Ada" });
        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");
        var accountId = (await (await session.GetAsync("/api/v1/email-accounts")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("id").GetString()!;

        var create = await session.PostAsync("/api/v1/templates",
            new { name = $"T {Guid.NewGuid():N}", description = (string?)null, content = content ?? DefaultContent() });
        create.EnsureSuccessStatusCode();
        var versionId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("currentVersion").GetProperty("id").GetString()!;

        return (session, accountId, versionId);
    }

    private static object DefaultContent() => new
    {
        editorKind = "html",
        subject = "Hi {{firstName}}",
        preheader = (string?)null,
        mjmlSource = (string?)null,
        grapesProject = (object?)null,
        htmlBody = "<p>Hello {{firstName}}</p>",
        textBody = (string?)null,
        variables = new[] { new { name = "firstName", type = "text", required = false, @default = "there", sample = "Ada" } },
        assets = Array.Empty<object>(),
    };

    private static object SendBody(string accountId, string versionId, object[] recipients, object? variables = null) => new
    {
        connectedEmailAccountId = accountId,
        templateVersionId = versionId,
        recipients,
        variables = variables ?? new { },
        attachments = Array.Empty<object>(),
        scheduledAt = (string?)null,
    };

    [Fact]
    public async Task Send_happy_path_delivers_and_marks_sent()
    {
        var (session, accountId, versionId) = await SetupAsync();

        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
            [new { email = "a@b.com", name = "A", contactId = (string?)null, variableOverrides = new { firstName = "Ada" } }]));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", detail.GetProperty("job").GetProperty("status").GetString());
        Assert.Equal("sent", detail.GetProperty("recipients")[0].GetProperty("status").GetString());

        Assert.True(factory.Provider.Sent.TryDequeue(out var email));
        Assert.Equal("a@b.com", email!.To[0].Email);
        Assert.Contains("Ada", email.HtmlBody);
    }

    [Fact]
    public async Task Transient_failure_marks_job_retrying()
    {
        var (session, accountId, versionId) = await SetupAsync();
        factory.Provider.Behavior = _ =>
            throw new ProviderSendException(ProviderErrorKind.Transient, "temporary");

        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
            [new { email = "a@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null }]));
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("retrying", detail.GetProperty("job").GetProperty("status").GetString());
        Assert.Equal("pending", detail.GetProperty("recipients")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Permanent_failure_marks_recipient_failed()
    {
        var (session, accountId, versionId) = await SetupAsync();
        factory.Provider.Behavior = _ =>
            throw new ProviderSendException(ProviderErrorKind.RecipientRejected, "bad address");

        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
            [new { email = "bad@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null }]));
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", detail.GetProperty("job").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Partial_failure_is_reported()
    {
        var (session, accountId, versionId) = await SetupAsync();
        factory.Provider.Behavior = email =>
            email.To[0].Email == "bad@b.com"
                ? throw new ProviderSendException(ProviderErrorKind.RecipientRejected, "bad")
                : new ProviderSendResult("ok", null, "sent");

        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
        [
            new { email = "good@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null },
            new { email = "bad@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null },
        ]));
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("partiallyfailed", detail.GetProperty("job").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Manual_retry_re_sends_failed_recipients()
    {
        var (session, accountId, versionId) = await SetupAsync();
        factory.Provider.Behavior = _ =>
            throw new ProviderSendException(ProviderErrorKind.RecipientRejected, "bad");

        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
            [new { email = "a@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null }]));
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Now succeed, then retry.
        factory.Provider.Behavior = _ => new ProviderSendResult("ok", null, "sent");
        var retry = await session.PostAsync($"/api/v1/sends/{jobId}/retry");
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", detail.GetProperty("job").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Missing_required_variable_per_recipient_is_422()
    {
        var content = new
        {
            editorKind = "html", subject = "Hi {{firstName}}", preheader = (string?)null,
            mjmlSource = (string?)null, grapesProject = (object?)null,
            htmlBody = "<p>{{firstName}}</p>", textBody = (string?)null,
            variables = new[] { new { name = "firstName", type = "text", required = true, @default = (string?)null, sample = "Ada" } },
            assets = Array.Empty<object>(),
        };
        var (session, accountId, versionId) = await SetupAsync(content);

        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
            [new { email = "a@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null }]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("send.variables_missing", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Test_send_delivers_to_self_with_test_prefix()
    {
        var (session, accountId, versionId) = await SetupAsync();

        var response = await session.PostAsync("/api/v1/sends/test",
            new { connectedEmailAccountId = accountId, templateVersionId = versionId, variables = new { }, attachments = Array.Empty<object>(), toSelf = "account" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        Assert.True(factory.Provider.Sent.TryDequeue(out var email));
        Assert.StartsWith("[TEST]", email!.Subject);
        Assert.Equal("sender@gmail.com", email.To[0].Email); // account address
    }

    [Fact]
    public async Task Scheduled_send_is_not_sent_immediately_and_can_be_cancelled()
    {
        var (session, accountId, versionId) = await SetupAsync();

        var body = new
        {
            connectedEmailAccountId = accountId, templateVersionId = versionId,
            recipients = new[] { new { email = "a@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null } },
            variables = new { }, attachments = Array.Empty<object>(),
            scheduledAt = DateTimeOffset.UtcNow.AddHours(1),
        };
        var response = await session.PostAsync("/api/v1/sends", body);
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var detail = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("scheduled", detail.GetProperty("job").GetProperty("status").GetString());
        Assert.Empty(factory.Provider.Sent);

        var cancel = await session.PostAsync($"/api/v1/sends/{jobId}/cancel");
        Assert.Equal(HttpStatusCode.Accepted, cancel.StatusCode);
        var cancelled = await (await session.GetAsync($"/api/v1/sends/{jobId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cancelled", cancelled.GetProperty("job").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sends_are_isolated_between_users()
    {
        var (session, accountId, versionId) = await SetupAsync();
        var response = await session.PostAsync("/api/v1/sends", SendBody(accountId, versionId,
            [new { email = "a@b.com", name = (string?)null, contactId = (string?)null, variableOverrides = (object?)null }]));
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var stranger = NewSession();
        await stranger.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Eve" });
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/sends/{jobId}")).StatusCode);
    }
}
