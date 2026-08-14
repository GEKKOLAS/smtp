using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Ai;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MailTemplateHub.IntegrationTests;

public class AiImageGenerationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    // A real 1x1 PNG the fake generator returns.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private TestSession NewSession(WebApplicationFactory<Program> f) => new(f.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private async Task<TestSession> RegisteredSessionAsync(WebApplicationFactory<Program> f)
    {
        var session = NewSession(f);
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });
        return session;
    }

    [Fact]
    public async Task Capabilities_reports_image_generation_disabled_without_key()
    {
        var session = await RegisteredSessionAsync(factory);
        var body = await (await session.GetAsync("/api/v1/ai/capabilities")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("imageGeneration").GetBoolean());
    }

    [Fact]
    public async Task Generate_image_is_unprocessable_when_not_configured()
    {
        var session = await RegisteredSessionAsync(factory);
        var response = await session.PostAsync("/api/v1/ai/images/generate", new { prompt = "a red circle" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await NewSession(factory).PostAsync("/api/v1/ai/images/generate", new { prompt = "hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Generate_image_creates_a_ready_public_asset()
    {
        // Swap in a fake generator so no real OpenAI call happens.
        var configured = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAiImageGenerator>();
            s.AddScoped<IAiImageGenerator>(_ => new FakeImageGenerator(OnePixelPng));
        }));

        var session = await RegisteredSessionAsync(configured);

        var response = await session.PostAsync("/api/v1/ai/images/generate",
            new { prompt = "a minimalist geometric logo" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var asset = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("image", asset.GetProperty("kind").GetString());
        Assert.Equal("public", asset.GetProperty("access").GetString());
        Assert.False(string.IsNullOrEmpty(asset.GetProperty("publicUrl").GetString()));
        var id = asset.GetProperty("id").GetString();

        // It shows up in the image asset library.
        var list = await (await session.GetAsync("/api/v1/assets?kind=image&page=1&pageSize=24"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.GetProperty("items").EnumerateArray(),
            a => a.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task Empty_prompt_is_rejected()
    {
        var configured = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAiImageGenerator>();
            s.AddScoped<IAiImageGenerator>(_ => new FakeImageGenerator(OnePixelPng));
        }));
        var session = await RegisteredSessionAsync(configured);
        var response = await session.PostAsync("/api/v1/ai/images/generate", new { prompt = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private sealed class FakeImageGenerator(byte[] png) : IAiImageGenerator
    {
        public bool IsConfigured => true;
        public Task<GeneratedImage> GenerateAsync(string prompt, string? size, CancellationToken ct) =>
            Task.FromResult(new GeneratedImage(png, "image/png"));
    }
}
