using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.IntegrationTests;

public class AccountsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ExtractState(string authorizationUrl)
    {
        var query = new Uri(authorizationUrl).Query.TrimStart('?');
        var pair = query.Split('&').Single(p => p.StartsWith("state=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(pair["state=".Length..]);
    }

    /// <summary>Registers a user and connects one Gmail account, returning the session and account id.</summary>
    private async Task<(TestSession Session, string AccountId)> ConnectedGmailAsync(string sub, string email)
    {
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ada" });

        var start = await session.GetAsync("/api/v1/oauth/gmail/start");
        var state = ExtractState((await start.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("authorizationUrl").GetString()!);

        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/token", new
        {
            access_token = "google-access-token",
            refresh_token = "google-refresh-token",
            expires_in = 3600,
            scope = "openid email profile https://www.googleapis.com/auth/gmail.send",
            token_type = "Bearer",
        });
        factory.OAuth.OnGet("google/userinfo", new { sub, email, name = "Ada" });

        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");

        var accounts = await session.GetAsync("/api/v1/email-accounts");
        var id = (await accounts.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("id").GetString()!;
        return (session, id);
    }

    [Fact]
    public async Task Set_default_marks_exactly_one_account()
    {
        var (session, accountId) = await ConnectedGmailAsync("sub-default", "default@gmail.com");

        var response = await session.PostAsync($"/api/v1/email-accounts/{accountId}/default");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var accounts = await session.GetAsync("/api/v1/email-accounts");
        var account = (await accounts.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0];
        Assert.True(account.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task Test_endpoint_returns_profile_for_active_account()
    {
        var (session, accountId) = await ConnectedGmailAsync("sub-test", "test@gmail.com");

        // Access token is still fresh, so no refresh; only the profile call fires.
        factory.OAuth.Reset();
        factory.OAuth.OnGet("google/userinfo", new { sub = "sub-test", email = "test@gmail.com", name = "Ada" });

        var response = await session.PostAsync($"/api/v1/email-accounts/{accountId}/test");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("test@gmail.com", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Disconnect_wipes_tokens_and_marks_revoked()
    {
        var (session, accountId) = await ConnectedGmailAsync("sub-disc", "disc@gmail.com");

        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/revoke", new { });

        var response = await session.SendAsync(HttpMethod.Delete, $"/api/v1/email-accounts/{accountId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Soft-deleted account no longer appears in the list.
        var accounts = await session.GetAsync("/api/v1/email-accounts");
        var items = (await accounts.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(0, items.GetArrayLength());

        // Token row is physically removed.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accountGuid = Guid.Parse(accountId);
        Assert.False(await db.OAuthTokens
            .AnyAsync(t => t.ConnectedEmailAccountId == accountGuid, CancellationToken.None));
    }

    [Fact]
    public async Task Accounts_are_isolated_between_users()
    {
        var (_, accountId) = await ConnectedGmailAsync("sub-owner", "owner@gmail.com");

        var stranger = NewSession();
        await stranger.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Eve" });

        // Another user cannot see or act on the account (404, not 403).
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/v1/email-accounts/{accountId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.SendAsync(HttpMethod.Delete, $"/api/v1/email-accounts/{accountId}")).StatusCode);

        var strangerAccounts = await stranger.GetAsync("/api/v1/email-accounts");
        Assert.Equal(0, (await strangerAccounts.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());
    }
}
