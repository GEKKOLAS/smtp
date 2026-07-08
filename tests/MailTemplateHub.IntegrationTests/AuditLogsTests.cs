using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class AuditLogsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_and_login_produce_audit_entries_for_that_user_only()
    {
        var session = NewSession();
        var email = UniqueEmail();
        await session.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Ada" });
        // A second login adds another event.
        await session.PostAsync("/api/v1/auth/login", new { email, password = Password });

        var response = await session.GetAsync("/api/v1/audit-logs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var actions = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("action").GetString())
            .ToList();
        Assert.Contains("auth.register", actions);
        Assert.Contains("auth.login", actions);
    }

    [Fact]
    public async Task Action_filter_narrows_results()
    {
        var session = NewSession();
        var email = UniqueEmail();
        await session.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Ada" });

        var filtered = await session.GetAsync("/api/v1/audit-logs?action=auth.register");
        var items = (await filtered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.All(items.EnumerateArray(), i => Assert.Equal("auth.register", i.GetProperty("action").GetString()));
    }

    [Fact]
    public async Task Audit_logs_are_isolated_between_users()
    {
        var owner = NewSession();
        await owner.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ada" });

        var stranger = NewSession();
        await stranger.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Eve" });

        // Each user only sees their own single register event.
        var strangerLogs = (await (await stranger.GetAsync("/api/v1/audit-logs")).Content
            .ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.All(strangerLogs.EnumerateArray(),
            i => Assert.Equal("auth.register", i.GetProperty("action").GetString()));
        Assert.Equal(1, strangerLogs.GetArrayLength());
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await NewSession().GetAsync("/api/v1/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
