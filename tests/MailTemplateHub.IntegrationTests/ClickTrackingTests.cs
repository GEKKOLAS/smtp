using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MailTemplateHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.IntegrationTests;

public partial class ClickTrackingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery staple";
    private const string Chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Safari/537.36";

    private TestSession NewSession(WebApplicationFactory<Program>? app = null) => new((app ?? factory).CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private HttpClient Anonymous() => factory.CreateClient(
        new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    private static string ExtractState(string url)
    {
        var query = new Uri(url).Query.TrimStart('?');
        return Uri.UnescapeDataString(query.Split('&').Single(p => p.StartsWith("state=")).Substring("state=".Length));
    }

    private static object Content() => new
    {
        editorKind = "html",
        subject = "Spring menu",
        preheader = (string?)null,
        mjmlSource = (string?)null,
        grapesProject = (object?)null,
        htmlBody = """
            <p>Hi {{firstName}}</p>
            <p><a href="https://shop.test/menu?x=1">See the menu</a> or <a href="https://shop.test/menu?x=1">again</a></p>
            <p><a href="mailto:hello@shop.test">Write to us</a> · <a href="https://shop.test/unsubscribe">Unsubscribe</a></p>
            """,
        textBody = (string?)null,
        variables = new[] { new { name = "firstName", type = "text", required = false, @default = "there", sample = "Ada" } },
        assets = Array.Empty<object>(),
    };

    /// <summary>Registers, connects Gmail, creates the template, sends it to one recipient.</summary>
    private async Task<(TestSession Session, string JobId, string Html, string Text)> SendAsync(
        bool trackClicks, string? utmCampaign)
    {
        factory.Provider.Reset();
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });

        var start = await session.GetAsync("/api/v1/oauth/gmail/start");
        var state = ExtractState((await start.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("authorizationUrl").GetString()!);
        factory.OAuth.Reset();
        factory.OAuth.OnPost("google/token", new
        {
            access_token = "google-access", refresh_token = "google-refresh", expires_in = 3600,
            scope = "openid email profile https://www.googleapis.com/auth/gmail.send", token_type = "Bearer",
        });
        factory.OAuth.OnGet("google/userinfo", new { sub = $"sub-{Guid.NewGuid():N}", email = "sender@gmail.com", name = "Ada" });
        await session.GetAsync($"/api/v1/oauth/gmail/callback?code=c&state={state}");
        var accountId = (await (await session.GetAsync("/api/v1/email-accounts")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("id").GetString()!;

        var create = await session.PostAsync("/api/v1/templates",
            new { name = $"T {Guid.NewGuid():N}", description = (string?)null, content = Content() });
        create.EnsureSuccessStatusCode();
        var versionId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("currentVersion").GetProperty("id").GetString()!;

        var send = await session.PostAsync("/api/v1/sends", new
        {
            connectedEmailAccountId = accountId,
            templateVersionId = versionId,
            recipients = new[] { new { email = "ada@example.com", name = "Ada", contactId = (string?)null, variableOverrides = (object?)null } },
            variables = new { },
            attachments = Array.Empty<object>(),
            scheduledAt = (string?)null,
            trackClicks,
            utmCampaign,
        });
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);
        var job = await send.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(trackClicks, job.GetProperty("trackClicks").GetBoolean());

        Assert.True(factory.Provider.Sent.TryDequeue(out var email));
        return (session, job.GetProperty("id").GetString()!, email!.HtmlBody, email.TextBody);
    }

    private async Task<int> ClicksAsync(string token, string classification)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.LinkClicks.CountAsync(c =>
            c.Classification == classification && db.TrackedLinks.Any(l => l.Id == c.TrackedLinkId && l.Token == token));
    }

    private async Task<HttpResponseMessage> FollowAsync(HttpMethod method, string path, string userAgent)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return await Anonymous().SendAsync(request);
    }

    [Fact]
    public async Task Tracked_send_rewrites_links_and_the_redirect_records_human_clicks()
    {
        var (_, _, html, text) = await SendAsync(trackClicks: true, utmCampaign: "spring launch");

        // Both copies of the menu link share one token; mailto and unsubscribe are untouched.
        var tokens = TrackedUrlRegex().Matches(html).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Distinct());
        Assert.Contains("mailto:hello@shop.test", html);
        Assert.Contains("href=\"https://shop.test/unsubscribe\"", html);
        Assert.DoesNotContain("shop.test/menu", html);
        Assert.Contains($"https://track.test/api/v1/t/{tokens[0]}", text);

        var response = await FollowAsync(HttpMethod.Get, $"/api/v1/t/{tokens[0]}", Chrome);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://shop.test/menu?x=1&utm_source=mail-template-hub&utm_medium=email&utm_campaign=spring%20launch",
            response.Headers.Location?.OriginalString);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(1, await ClicksAsync(tokens[0], "human"));
    }

    [Fact]
    public async Task Scanner_clicks_are_labeled_and_head_requests_are_not_counted()
    {
        var (_, _, html, _) = await SendAsync(trackClicks: true, utmCampaign: null);
        var token = TrackedUrlRegex().Match(html).Groups[1].Value;

        var head = await FollowAsync(HttpMethod.Head, $"/api/v1/t/{token}", Chrome);
        var scanner = await FollowAsync(HttpMethod.Get, $"/api/v1/t/{token}", "Microsoft Office/16.0 (Microsoft Outlook)");

        Assert.Equal(HttpStatusCode.Redirect, head.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, scanner.StatusCode);
        Assert.Equal("https://shop.test/menu?x=1", scanner.Headers.Location?.OriginalString);
        Assert.Equal(0, await ClicksAsync(token, "human"));
        Assert.Equal(1, await ClicksAsync(token, "scanner"));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("not-a-token")]
    public async Task Unknown_tokens_get_a_404_page(string token)
    {
        var response = await FollowAsync(HttpMethod.Get, $"/api/v1/t/{token}", Chrome);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Utm_only_send_tags_links_without_redirects()
    {
        var (_, _, html, _) = await SendAsync(trackClicks: false, utmCampaign: "spring");

        Assert.DoesNotContain("track.test", html);
        Assert.Contains("https://shop.test/menu?x=1&amp;utm_source=mail-template-hub&amp;utm_medium=email&amp;utm_campaign=spring", html);
    }

    [Fact]
    public async Task Invalid_campaign_names_are_rejected()
    {
        var session = NewSession();
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });

        var response = await session.PostAsync("/api/v1/sends", new
        {
            connectedEmailAccountId = Guid.NewGuid(), templateVersionId = Guid.NewGuid(),
            recipients = Array.Empty<object>(), utmCampaign = "<script>",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Tracking_is_refused_without_a_public_base_url()
    {
        using var untracked = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Tracking:PublicBaseUrl"] = "" })));
        var session = NewSession(untracked);
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Ada" });

        var status = await (await session.GetAsync("/api/v1/tracking/status")).Content.ReadFromJsonAsync<JsonElement>();
        var response = await session.PostAsync("/api/v1/sends", new
        {
            connectedEmailAccountId = Guid.NewGuid(), templateVersionId = Guid.NewGuid(),
            recipients = Array.Empty<object>(), trackClicks = true,
        });

        Assert.False(status.GetProperty("clickTracking").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("tracking.unavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Analytics_count_people_not_scanners_and_strip_our_utm_tags()
    {
        var (session, jobId, html, _) = await SendAsync(trackClicks: true, utmCampaign: "spring");
        var token = TrackedUrlRegex().Match(html).Groups[1].Value;
        await FollowAsync(HttpMethod.Get, $"/api/v1/t/{token}", "Mozilla/5.0 (compatible; Mimecast)");
        await FollowAsync(HttpMethod.Get, $"/api/v1/t/{token}", Chrome);
        await FollowAsync(HttpMethod.Get, $"/api/v1/t/{token}", Chrome);

        var send = await (await session.GetAsync($"/api/v1/sends/{jobId}/analytics")).Content.ReadFromJsonAsync<JsonElement>();
        var totals = send.GetProperty("totals");
        Assert.Equal(1, totals.GetProperty("accepted").GetInt32());
        Assert.Equal(1, totals.GetProperty("trackedRecipients").GetInt32());
        Assert.Equal(1, totals.GetProperty("uniqueClickers").GetInt32());
        Assert.Equal(1.0, totals.GetProperty("clickRate").GetDouble());
        var link = Assert.Single(send.GetProperty("links").EnumerateArray());
        Assert.Equal("https://shop.test/menu?x=1", link.GetProperty("url").GetString());
        Assert.Equal(2, link.GetProperty("clicks").GetInt32());
        Assert.Equal(1, link.GetProperty("scannerClicks").GetInt32());
        Assert.True(send.GetProperty("recipients")[0].GetProperty("clicked").GetBoolean());

        var summary = await (await session.GetAsync("/api/v1/analytics/summary?days=7")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, summary.GetProperty("totals").GetProperty("sends").GetInt32());
        Assert.Equal(8, summary.GetProperty("daily").GetArrayLength());
        Assert.Equal(1, summary.GetProperty("templates")[0].GetProperty("uniqueClickers").GetInt32());
        Assert.Equal("https://shop.test/menu?x=1", summary.GetProperty("topLinks")[0].GetProperty("url").GetString());

        var stranger = NewSession();
        await stranger.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password, displayName = "Eve" });
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/sends/{jobId}/analytics")).StatusCode);
        var empty = await (await stranger.GetAsync("/api/v1/analytics/summary")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, empty.GetProperty("totals").GetProperty("sends").GetInt32());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("totals").GetProperty("clickRate").ValueKind);
    }

    [GeneratedRegex(@"https://track\.test/api/v1/t/([A-Za-z0-9_-]{22})")]
    private static partial Regex TrackedUrlRegex();
}
