using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MailTemplateHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.IntegrationTests;

public class OAuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string ExtractState(string authorizationUrl)
    {
        var query = new Uri(authorizationUrl).Query.TrimStart('?');
        var pair = query.Split('&').Single(p => p.StartsWith("state=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(pair["state=".Length..]);
    }

    private async Task<string> StartAsync(TestSession session, string provider)
    {
        var response = await session.GetAsync($"/api/v1/oauth/{provider}/start");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return ExtractState(body.GetProperty("authorizationUrl").GetString()!);
    }

    private void StubGoogleHappyPath(string sub, string email, string scope =
        "openid email profile https://www.googleapis.com/auth/gmail.send")
    {
        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/token", new
        {
            access_token = "google-access-token",
            refresh_token = "google-refresh-token",
            expires_in = 3600,
            scope,
            token_type = "Bearer",
        });
        factory.OAuth.OnGet("google/userinfo", new { sub, email, name = "Ada Lovelace" });
    }

    private static string FakeJwt(object payload)
    {
        static string Segment(object o) => Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(o)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Segment(new { alg = "none" })}.{Segment(payload)}.sig";
    }

    [Fact]
    public async Task Connect_gmail_happy_path_stores_encrypted_tokens_and_lists_account()
    {
        var session = await RegisteredSessionAsync();
        var state = await StartAsync(session, "gmail");
        StubGoogleHappyPath("google-sub-1", "ada@gmail.com");

        var callback = await session.GetAsync($"/api/v1/oauth/gmail/callback?code=auth-code&state={state}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("connected=gmail", callback.Headers.Location!.ToString());

        var accounts = await session.GetAsync("/api/v1/email-accounts");
        var items = (await accounts.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        var account = items.EnumerateArray().Single();
        Assert.Equal("gmail", account.GetProperty("provider").GetString());
        Assert.Equal("ada@gmail.com", account.GetProperty("emailAddress").GetString());
        Assert.Equal("active", account.GetProperty("state").GetString());

        // No token material is ever exposed in the API surface.
        Assert.False(account.TryGetProperty("accessToken", out _));
        Assert.False(account.TryGetProperty("token", out _));

        // Tokens are encrypted at rest: ciphertext must not contain the plaintext.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ConnectedEmailAccounts
            .SingleAsync(a => a.EmailAddress == "ada@gmail.com", CancellationToken.None);
        var token = await db.OAuthTokens
            .SingleAsync(t => t.ConnectedEmailAccountId == stored.Id, CancellationToken.None);
        Assert.DoesNotContain("google-access-token",
            Encoding.UTF8.GetString(token.AccessTokenCiphertext));
        Assert.NotNull(token.RefreshTokenCiphertext);
    }

    [Fact]
    public async Task Connect_outlook_happy_path_captures_tenant_from_id_token()
    {
        var session = await RegisteredSessionAsync();
        var state = await StartAsync(session, "outlook");

        factory.OAuth.Reset();
        factory.OAuth.OnPost("ms/token", new
        {
            access_token = "ms-access-token",
            refresh_token = "ms-refresh-token",
            expires_in = 3600,
            scope = "openid offline_access User.Read Mail.Send",
            token_type = "Bearer",
            id_token = FakeJwt(new { tid = "tenant-42", oid = "ms-oid-1" }),
        });
        factory.OAuth.OnGet("ms/me", new { id = "graph-id", mail = "ada@outlook.com", displayName = "Ada" });

        var callback = await session.GetAsync($"/api/v1/oauth/outlook/callback?code=auth-code&state={state}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("connected=outlook", callback.Headers.Location!.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.ConnectedEmailAccounts
            .SingleAsync(a => a.EmailAddress == "ada@outlook.com", CancellationToken.None);
        Assert.Equal("tenant-42", account.TenantId);
        Assert.Equal("ms-oid-1", account.ProviderAccountId); // oid preferred over Graph id
    }

    [Fact]
    public async Task Reused_state_is_rejected()
    {
        var session = await RegisteredSessionAsync();
        var state = await StartAsync(session, "gmail");
        StubGoogleHappyPath("google-sub-reuse", "reuse@gmail.com");

        var first = await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");
        Assert.Contains("connected=gmail", first.Headers.Location!.ToString());

        // State is single-use; replaying it must fail.
        var second = await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains("error=oauth.state_invalid", second.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Unknown_state_is_rejected()
    {
        var session = await RegisteredSessionAsync();
        var response = await session.GetAsync(
            $"/api/v1/oauth/gmail/callback?code=c&state={Guid.NewGuid()}");

        Assert.Contains("error=oauth.state_invalid", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task State_from_another_user_is_rejected()
    {
        var userA = await RegisteredSessionAsync();
        var state = await StartAsync(userA, "gmail");

        // A different user's session cannot complete user A's flow.
        var userB = await RegisteredSessionAsync();
        StubGoogleHappyPath("google-sub-foreign", "foreign@gmail.com");
        var response = await userB.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");

        Assert.Contains("error=oauth.state_invalid", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Missing_send_scope_marks_account_needs_reconnect()
    {
        var session = await RegisteredSessionAsync();
        var state = await StartAsync(session, "gmail");
        // User unchecked gmail.send at the consent screen.
        StubGoogleHappyPath("google-sub-scope", "scope@gmail.com", scope: "openid email profile");

        var callback = await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");
        Assert.Contains("error=oauth.scope_missing", callback.Headers.Location!.ToString());

        var accounts = await session.GetAsync("/api/v1/email-accounts");
        var account = (await accounts.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().Single();
        Assert.Equal("needs_reconnect", account.GetProperty("state").GetString());
        Assert.Equal("insufficient_scope", account.GetProperty("stateReason").GetString());
    }

    [Fact]
    public async Task Reconnecting_same_provider_account_updates_in_place()
    {
        var session = await RegisteredSessionAsync();

        var state1 = await StartAsync(session, "gmail");
        StubGoogleHappyPath("google-sub-same", "old@gmail.com");
        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state1}");

        var state2 = await StartAsync(session, "gmail");
        StubGoogleHappyPath("google-sub-same", "new@gmail.com"); // same sub, new email
        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state2}");

        var accounts = await session.GetAsync("/api/v1/email-accounts");
        var items = (await accounts.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength()); // upsert, not duplicate
        Assert.Equal("new@gmail.com", items[0].GetProperty("emailAddress").GetString());
    }

    [Fact]
    public async Task Callback_requires_authentication()
    {
        var response = await NewSession().GetAsync(
            $"/api/v1/oauth/gmail/callback?code=c&state={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
