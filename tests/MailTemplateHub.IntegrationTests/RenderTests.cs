using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class RenderTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static object InlineContent(string html, object[] variables) => new
    {
        editorKind = "html",
        subject = "Hi {{firstName}}",
        preheader = (string?)null,
        mjmlSource = (string?)null,
        grapesProject = (object?)null,
        htmlBody = html,
        textBody = (string?)null,
        variables,
        assets = Array.Empty<object>(),
    };

    [Fact]
    public async Task Preview_sample_mode_fills_from_sample_values()
    {
        var session = await RegisteredSessionAsync();
        var content = InlineContent("<p>Hello {{firstName}}</p>",
            [new { name = "firstName", type = "text", required = true, @default = (string?)null, sample = "Sample Ada" }]);

        var response = await session.PostAsync("/api/v1/render/preview",
            new { templateVersionId = (string?)null, content, variables = new { }, mode = "sample" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Sample Ada", result.GetProperty("html").GetString());
        Assert.Equal("Hi Sample Ada", result.GetProperty("subject").GetString());
        Assert.Contains("Sample Ada", result.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Preview_strict_mode_422s_on_missing_required_variable()
    {
        var session = await RegisteredSessionAsync();
        var content = InlineContent("<p>{{firstName}}</p>",
            [new { name = "firstName", type = "text", required = true, @default = (string?)null, sample = "Ada" }]);

        var response = await session.PostAsync("/api/v1/render/preview",
            new { templateVersionId = (string?)null, content, variables = new { }, mode = "strict" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("template.variables_missing", body.GetProperty("errorCode").GetString());
        Assert.Contains("firstName", body.GetProperty("missingVariables").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Preview_encodes_text_variables()
    {
        var session = await RegisteredSessionAsync();
        var content = InlineContent("<p>{{name}}</p>",
            [new { name = "name", type = "text", required = false, @default = (string?)null, sample = "x" }]);

        var response = await session.PostAsync("/api/v1/render/preview",
            new { templateVersionId = (string?)null, content, variables = new { name = "<script>bad</script>" }, mode = "sample" });

        var html = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("html").GetString()!;
        Assert.DoesNotContain("<script>bad", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task Validate_reports_mjml_errors_without_throwing()
    {
        var session = await RegisteredSessionAsync();
        var content = new
        {
            editorKind = "mjml",
            subject = "s",
            preheader = (string?)null,
            mjmlSource = "<mjml><mj-body><mj-bad></mj-bad></mj-body></mjml>",
            grapesProject = (object?)null,
            htmlBody = "",
            textBody = (string?)null,
            variables = Array.Empty<object>(),
            assets = Array.Empty<object>(),
        };

        var response = await session.PostAsync("/api/v1/render/validate",
            new { templateVersionId = (string?)null, content, variables = new { }, mode = "sample" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.True(result.GetProperty("errors").GetArrayLength() > 0);
    }
}
