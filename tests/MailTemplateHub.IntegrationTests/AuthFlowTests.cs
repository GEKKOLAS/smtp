using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MailTemplateHub.IntegrationTests;

public class AuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";

    private TestSession NewSession() => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false }));

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    [Fact]
    public async Task Register_sets_session_and_me_returns_profile()
    {
        var session = NewSession();
        var email = UniqueEmail();

        var register = await session.PostAsync("/api/v1/auth/register",
            new { email, password = Password, displayName = "Ada" });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.True(session.HasSessionCookie);
        Assert.NotNull(session.CsrfToken);

        var me = await session.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, (await Json(me)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Duplicate_register_returns_same_shape_without_session()
    {
        var email = UniqueEmail();
        await NewSession().PostAsync("/api/v1/auth/register",
            new { email, password = Password, displayName = "First" });

        var second = NewSession();
        var response = await second.PostAsync("/api/v1/auth/register",
            new { email, password = "another password 123", displayName = "Second" });

        // Indistinguishable status/shape, but no session established.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await Json(response)).TryGetProperty("user", out _));
        Assert.False(second.HasSessionCookie);

        // Original credentials still work — the duplicate attempt changed nothing.
        var login = await NewSession().PostAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_error_code()
    {
        var email = UniqueEmail();
        await NewSession().PostAsync("/api/v1/auth/register",
            new { email, password = Password, displayName = "Ada" });

        var response = await NewSession().PostAsync("/api/v1/auth/login",
            new { email, password = "wrong password entirely" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Logout_invalidates_the_session_server_side()
    {
        var session = NewSession();
        var email = UniqueEmail();
        await session.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Ada" });

        var stolenCookies = new Dictionary<string, string>(session.Cookies);

        var logout = await session.PostAsync("/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Even replaying the old cookie fails: the session row is gone.
        var replay = NewSession();
        foreach (var (name, value) in stolenCookies) replay.Cookies[name] = value;
        var me = await replay.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Mutations_without_csrf_header_are_rejected()
    {
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = Password, displayName = "Ada" });

        var response = await session.SendAsync(HttpMethod.Post, "/api/v1/me/password",
            new { currentPassword = Password, newPassword = "a new password 12345" },
            includeCsrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("csrf.invalid", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Change_password_keeps_current_session_and_revokes_others()
    {
        var email = UniqueEmail();
        var first = NewSession();
        await first.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Ada" });

        var second = NewSession();
        await second.PostAsync("/api/v1/auth/login", new { email, password = Password });

        const string newPassword = "brand new password 42";
        var change = await first.PostAsync("/api/v1/me/password",
            new { currentPassword = Password, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/v1/me")).StatusCode);

        var reLogin = await NewSession().PostAsync("/api/v1/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, reLogin.StatusCode);
    }

    [Fact]
    public async Task Password_reset_flow_end_to_end()
    {
        var email = UniqueEmail();
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Ada" });

        var forgot = await NewSession().PostAsync("/api/v1/auth/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        var (sentTo, token) = factory.EmailSender.Sent.Single(s => s.Email == email);
        Assert.Equal(email, sentTo);

        const string newPassword = "post-reset password 99";
        var reset = await NewSession().PostAsync("/api/v1/auth/password/reset", new { token, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // All pre-reset sessions are revoked; the token is single-use.
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/me")).StatusCode);
        var reuse = await NewSession().PostAsync("/api/v1/auth/password/reset",
            new { token, newPassword = "yet another password 1" });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var login = await NewSession().PostAsync("/api/v1/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_forgot_password_returns_identical_202()
    {
        var response = await NewSession().PostAsync("/api/v1/auth/password/forgot",
            new { email = UniqueEmail() });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_can_be_listed_and_revoked()
    {
        var email = UniqueEmail();
        var first = NewSession();
        await first.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Ada" });
        var second = NewSession();
        await second.PostAsync("/api/v1/auth/login", new { email, password = Password });

        var list = await Json(await first.GetAsync("/api/v1/auth/sessions"));
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Single(items, i => i.GetProperty("current").GetBoolean());

        var otherId = items.Single(i => !i.GetProperty("current").GetBoolean()).GetProperty("id").GetString();
        var revoke = await first.SendAsync(HttpMethod.Delete, $"/api/v1/auth/sessions/{otherId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Weak_password_returns_422_with_field_errors()
    {
        var response = await NewSession().PostAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = "short", displayName = "Ada" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("validation_failed", body.GetProperty("errorCode").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Login_attempts_beyond_the_limit_get_429()
    {
        using var limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Auth:PermitLimit"] = "3",
                })));

        var client = limited.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = new TestSession(client);

        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await session.PostAsync("/api/v1/auth/login",
                new { email = "nobody@example.com", password = "does not matter 123" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
