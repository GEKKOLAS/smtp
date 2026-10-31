using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Ai;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class CopyAssistantTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private async Task<TestSession> RegisteredSessionAsync()
    {
        var session = new TestSession(factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });
        return session;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Variations_that_break_placeholders_are_discarded()
    {
        var session = await RegisteredSessionAsync();
        factory.CopyWriter.IsConfigured = true;
        factory.CopyWriter.Next =
        [
            new("{{firstName}}, your spring menu is here", "Leads with the name."),
            new("Your spring menu is here", "Dropped the name."),                  // drops {{firstName}}
            new("{{firstName}}, {{city}} gets the spring menu", "Adds a city."),   // invents {{city}}
            new("Hi {{ firstName }}, spring is on the menu", "Softer opening."),   // whitespace is fine
            new("Hi {{firstName}}, the spring menu is here", null),                // identical to the input
        ];

        var response = await session.PostAsync("/api/v1/ai/copy/variations",
            new { kind = "subject", text = "Hi {{firstName}}, the spring menu is here", tone = "promotional", count = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var texts = (await Json(response)).GetProperty("variations").EnumerateArray()
            .Select(v => v.GetProperty("text").GetString()).ToList();
        Assert.Equal(["{{firstName}}, your spring menu is here", "Hi {{ firstName }}, spring is on the menu"], texts);
    }

    [Fact]
    public async Task All_broken_variations_return_422()
    {
        var session = await RegisteredSessionAsync();
        factory.CopyWriter.IsConfigured = true;
        factory.CopyWriter.Next = [new("Your spring menu is here", null)];

        var response = await session.PostAsync("/api/v1/ai/copy/variations",
            new { kind = "subject", text = "Hi {{firstName}}, the spring menu is here", tone = "professional" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ai.generation_failed", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Brand_voice_and_context_reach_the_writer()
    {
        var session = await RegisteredSessionAsync();
        var kit = await session.PostAsync("/api/v1/brand-kits", new
        {
            name = "Acme",
            colors = new
            {
                primary = "#C2410C", secondary = "#00CEC9", background = "#FFF4E6", surface = "#FFFFFF",
                text = "#17151F", muted = "#6B6878", link = "#4B2FD6",
            },
            headingFont = "georgia",
            bodyFont = "verdana",
            button = new { radius = 4, style = "filled" },
            radius = new { small = 4, medium = 8, large = 16 },
            spacing = new { small = 8, medium = 16, large = 24 },
            voice = "Warm, short sentences.",
        });
        var kitId = (await Json(kit)).GetProperty("id").GetString();
        factory.CopyWriter.IsConfigured = true;
        factory.CopyWriter.Next = [new("Book your table", "Shorter.")];

        var response = await session.PostAsync("/api/v1/ai/copy/variations", new
        {
            kind = "cta", text = "Reserve a table now", tone = "followUp", count = 2, brandKitId = kitId,
            context = "  Spring menu launch  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = factory.CopyWriter.LastRequest!;
        Assert.Equal(new CopyRequest("cta", "Reserve a table now", "followUp", 2, "Warm, short sentences.",
            "Spring menu launch"), request);
    }

    [Theory]
    [InlineData("headline", "promotional", 3)]
    [InlineData("subject", "pushy", 3)]
    [InlineData("subject", "promotional", 9)]
    public async Task Invalid_requests_return_422(string kind, string tone, int count)
    {
        var session = await RegisteredSessionAsync();

        var response = await session.PostAsync("/api/v1/ai/copy/variations",
            new { kind, text = "Hello", tone, count });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("validation_failed", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Subject_longer_than_200_characters_is_rejected()
    {
        var session = await RegisteredSessionAsync();

        var response = await session.PostAsync("/api/v1/ai/copy/variations",
            new { kind = "subject", text = new string('a', 201), tone = "professional" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Unconfigured_assistant_returns_503_and_capabilities_say_so()
    {
        var session = await RegisteredSessionAsync();
        factory.CopyWriter.IsConfigured = false;
        try
        {
            var capabilities = await Json(await session.GetAsync("/api/v1/ai/capabilities"));
            Assert.False(capabilities.GetProperty("copyAssistant").GetBoolean());

            var response = await session.PostAsync("/api/v1/ai/copy/variations",
                new { kind = "subject", text = "Hello", tone = "professional" });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("ai.unavailable", (await Json(response)).GetProperty("errorCode").GetString());
        }
        finally
        {
            factory.CopyWriter.IsConfigured = true;
        }
    }
}
