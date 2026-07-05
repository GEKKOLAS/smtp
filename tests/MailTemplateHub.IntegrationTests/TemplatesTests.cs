using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class TemplatesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<TestSession> RegisteredSessionAsync()
    {
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ada" });
        return session;
    }

    private static object HtmlContent(string subject = "Hi {{firstName}}", string html = "<p>Hello {{firstName}}</p>") => new
    {
        editorKind = "html",
        subject,
        preheader = (string?)null,
        mjmlSource = (string?)null,
        grapesProject = (object?)null,
        htmlBody = html,
        textBody = (string?)null,
        variables = new[] { new { name = "firstName", type = "text", required = true, @default = (string?)null, sample = "Ada" } },
        assets = Array.Empty<object>(),
    };

    private static object MjmlContent(string mjml) => new
    {
        editorKind = "mjml",
        subject = "MJML {{firstName}}",
        preheader = (string?)null,
        mjmlSource = mjml,
        grapesProject = (object?)null,
        htmlBody = "",
        textBody = (string?)null,
        variables = new[] { new { name = "firstName", type = "text", required = true, @default = (string?)null, sample = "Ada" } },
        assets = Array.Empty<object>(),
    };

    private static async Task<string> CreateTemplateAsync(TestSession session, string name, object? content = null)
    {
        var response = await session.PostAsync("/api/v1/templates",
            new { name, description = (string?)null, content = content ?? HtmlContent() });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Create_makes_template_with_version_one()
    {
        var session = await RegisteredSessionAsync();
        var response = await session.PostAsync("/api/v1/templates",
            new { name = "Welcome", description = "greeting", content = HtmlContent() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var template = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Welcome", template.GetProperty("name").GetString());
        Assert.Equal(1, template.GetProperty("currentVersion").GetProperty("versionNumber").GetInt32());
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        var session = await RegisteredSessionAsync();
        await CreateTemplateAsync(session, "Newsletter");

        var response = await session.PostAsync("/api/v1/templates",
            new { name = "Newsletter", description = (string?)null, content = HtmlContent() });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("template.name_taken",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Mjml_is_compiled_on_create()
    {
        var session = await RegisteredSessionAsync();
        var mjml = "<mjml><mj-body><mj-section><mj-column><mj-text>Hi {{firstName}}</mj-text></mj-column></mj-section></mj-body></mjml>";
        var id = await CreateTemplateAsync(session, "Mjml one", MjmlContent(mjml));

        var template = await (await session.GetAsync($"/api/v1/templates/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        var html = template.GetProperty("currentVersion").GetProperty("htmlBody").GetString()!;
        Assert.Contains("<table", html); // compiled to table layout
        Assert.Contains("{{firstName}}", html); // variables intact until render
    }

    [Fact]
    public async Task Invalid_mjml_is_rejected_with_positions()
    {
        var session = await RegisteredSessionAsync();
        var response = await session.PostAsync("/api/v1/templates",
            new { name = "Bad mjml", description = (string?)null, content = MjmlContent("<mjml><mj-body><mj-nope></mj-nope></mj-body></mjml>") });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("template.mjml_invalid", body.GetProperty("errorCode").GetString());
        Assert.True(body.TryGetProperty("mjmlErrors", out var errors) && errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Saving_creates_new_version_and_advances_current()
    {
        var session = await RegisteredSessionAsync();
        var id = await CreateTemplateAsync(session, "Versioned");

        var save = await session.PostAsync($"/api/v1/templates/{id}/versions",
            HtmlContent(subject: "Updated {{firstName}}", html: "<p>v2 {{firstName}}</p>"));
        Assert.Equal(HttpStatusCode.Created, save.StatusCode);
        Assert.Equal(2, (await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("versionNumber").GetInt32());

        var versions = await (await session.GetAsync($"/api/v1/templates/{id}/versions")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, versions.GetProperty("totalCount").GetInt32());

        var template = await (await session.GetAsync($"/api/v1/templates/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, template.GetProperty("currentVersion").GetProperty("versionNumber").GetInt32());
    }

    [Fact]
    public async Task Restore_creates_new_version_from_old_content()
    {
        var session = await RegisteredSessionAsync();
        var id = await CreateTemplateAsync(session, "Restorable", HtmlContent(html: "<p>original</p>"));
        var v1 = await (await session.GetAsync($"/api/v1/templates/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        var v1Id = v1.GetProperty("currentVersion").GetProperty("id").GetString();

        await session.PostAsync($"/api/v1/templates/{id}/versions", HtmlContent(html: "<p>changed</p>"));

        var restore = await session.PostAsync($"/api/v1/templates/{id}/versions/{v1Id}/restore");
        Assert.Equal(HttpStatusCode.Created, restore.StatusCode);
        var restored = await restore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, restored.GetProperty("versionNumber").GetInt32()); // new version, not v1
        Assert.Contains("original", restored.GetProperty("htmlBody").GetString());
    }

    [Fact]
    public async Task Duplicate_copies_latest_version()
    {
        var session = await RegisteredSessionAsync();
        var id = await CreateTemplateAsync(session, "Original");

        var response = await session.PostAsync($"/api/v1/templates/{id}/duplicate");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Copy of Original", copy.GetProperty("name").GetString());
        Assert.Equal(1, copy.GetProperty("currentVersion").GetProperty("versionNumber").GetInt32());
    }

    [Fact]
    public async Task Archive_hides_from_default_list()
    {
        var session = await RegisteredSessionAsync();
        var id = await CreateTemplateAsync(session, "Archivable");

        await session.PostAsync($"/api/v1/templates/{id}/archive");

        var active = await (await session.GetAsync("/api/v1/templates")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, active.GetProperty("totalCount").GetInt32());

        var archived = await (await session.GetAsync("/api/v1/templates?archived=true")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, archived.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Delete_is_soft_and_hides_template()
    {
        var session = await RegisteredSessionAsync();
        var id = await CreateTemplateAsync(session, "Deletable");

        var delete = await session.SendAsync(HttpMethod.Delete, $"/api/v1/templates/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await session.GetAsync($"/api/v1/templates/{id}")).StatusCode);
    }

    [Fact]
    public async Task Templates_are_isolated_between_users()
    {
        var owner = await RegisteredSessionAsync();
        var id = await CreateTemplateAsync(owner, "Owned");

        var stranger = await RegisteredSessionAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/templates/{id}")).StatusCode);
    }
}
