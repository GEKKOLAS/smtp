using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class ApiKeysTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private HttpClient RawClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    private async Task<TestSession> RegisteredSessionAsync()
    {
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });
        return session;
    }

    private static async Task<string> CreateKeyAsync(TestSession session)
    {
        var response = await session.PostAsync("/api/v1/api-keys", new { name = "n8n", expiresInDays = (int?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
    }

    [Fact]
    public async Task Created_key_authenticates_api_requests()
    {
        var session = await RegisteredSessionAsync();
        var secret = await CreateKeyAsync(session);
        Assert.StartsWith("mth_", secret);

        // Use the key (no cookie) to call an authenticated endpoint.
        using var client = RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/templates/generate")
        {
            Content = JsonContent.Create(new { prompt = "A newsletter about coffee" }),
        };
        request.Headers.Add("Authorization", $"Bearer {secret}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<mjml>", (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mjmlSource").GetString());
    }

    [Fact]
    public async Task Invalid_key_is_unauthorized()
    {
        using var client = RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/templates/generate")
        {
            Content = JsonContent.Create(new { prompt = "x" }),
        };
        request.Headers.Add("Authorization", "Bearer mth_not_a_real_key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Revoked_key_stops_working()
    {
        var session = await RegisteredSessionAsync();
        var create = await session.PostAsync("/api/v1/api-keys", new { name = "temp", expiresInDays = (int?)null });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("key").GetProperty("id").GetString();
        var secret = created.GetProperty("secret").GetString()!;

        await session.SendAsync(HttpMethod.Delete, $"/api/v1/api-keys/{id}");

        using var client = RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/templates");
        request.Headers.Add("Authorization", $"Bearer {secret}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Key_management_cannot_use_an_api_key()
    {
        var session = await RegisteredSessionAsync();
        var secret = await CreateKeyAsync(session);

        // The api-keys endpoints are session-only; a key cannot list/create keys.
        using var client = RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        request.Headers.Add("Authorization", $"Bearer {secret}");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task List_shows_prefix_not_secret()
    {
        var session = await RegisteredSessionAsync();
        await CreateKeyAsync(session);

        var list = await (await session.GetAsync("/api/v1/api-keys")).Content.ReadFromJsonAsync<JsonElement>();
        var item = list.GetProperty("items")[0];
        Assert.StartsWith("mth_", item.GetProperty("prefix").GetString());
        Assert.False(item.TryGetProperty("secret", out _));
        Assert.False(item.TryGetProperty("keyHash", out _));
    }
}
