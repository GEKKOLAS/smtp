using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class AiGenerationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private async Task<TestSession> RegisteredSessionAsync()
    {
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });
        return session;
    }

    [Fact]
    public async Task Generate_returns_compilable_template_with_preview()
    {
        var session = await RegisteredSessionAsync();

        var response = await session.PostAsync("/api/v1/ai/templates/generate",
            new { prompt = "A friendly welcome email for new subscribers", brandColor = "#0ea5e9" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrEmpty(body.GetProperty("subject").GetString()));
        Assert.Contains("<mjml>", body.GetProperty("mjmlSource").GetString());
        Assert.Contains("<table", body.GetProperty("htmlBody").GetString()); // compiled
        Assert.Contains("<table", body.GetProperty("previewHtml").GetString()); // rendered
        Assert.True(body.GetProperty("variables").GetArrayLength() > 0);
        Assert.False(body.GetProperty("aiGenerated").GetBoolean()); // scaffold fallback in tests
    }

    [Fact]
    public async Task Generated_content_can_be_saved_as_a_template()
    {
        var session = await RegisteredSessionAsync();
        var generated = await (await session.PostAsync("/api/v1/ai/templates/generate",
            new { prompt = "Product launch announcement" })).Content.ReadFromJsonAsync<JsonElement>();

        var create = await session.PostAsync("/api/v1/templates", new
        {
            name = $"AI {Guid.NewGuid():N}",
            description = (string?)null,
            content = new
            {
                editorKind = "mjml",
                subject = generated.GetProperty("subject").GetString(),
                preheader = (string?)null,
                mjmlSource = generated.GetProperty("mjmlSource").GetString(),
                grapesProject = (object?)null,
                htmlBody = "",
                textBody = (string?)null,
                variables = generated.GetProperty("variables").EnumerateArray()
                    .Select(v => new
                    {
                        name = v.GetProperty("name").GetString(),
                        type = v.GetProperty("type").GetString(),
                        required = false,
                        @default = (string?)null,
                        sample = v.GetProperty("sample").GetString(),
                    }).ToArray(),
                assets = Array.Empty<object>(),
            },
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Empty_prompt_is_rejected()
    {
        var session = await RegisteredSessionAsync();
        var response = await session.PostAsync("/api/v1/ai/templates/generate", new { prompt = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await NewSession().PostAsync("/api/v1/ai/templates/generate", new { prompt = "hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
