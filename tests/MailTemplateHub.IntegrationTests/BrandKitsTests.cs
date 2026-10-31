using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class BrandKitsTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static object Kit(string name, string primary = "#6C5CE7", Guid? logoAssetId = null) => new
    {
        name,
        colors = new
        {
            primary, secondary = "#00CEC9", background = "#101114", surface = "#1b1d23",
            text = "#F5F5F7", muted = "#9A9AA5", link = "#A29BFE",
        },
        headingFont = "space-grotesk",
        bodyFont = "inter",
        logoAssetId,
        button = new { radius = 12, style = "filled" },
        radius = new { small = 8, medium = 12, large = 20 },
        spacing = new { small = 8, medium = 16, large = 24 },
        voice = "Warm, direct, no emojis.",
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> CreateAsync(TestSession session, object kit)
    {
        var response = await session.PostAsync("/api/v1/brand-kits", kit);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    [Fact]
    public async Task First_kit_becomes_default_and_round_trips()
    {
        var session = await RegisteredSessionAsync();

        var kit = await CreateAsync(session, Kit("Acme"));

        Assert.True(kit.GetProperty("isDefault").GetBoolean());
        // Hex colors are normalized to upper case.
        Assert.Equal("#1B1D23", kit.GetProperty("colors").GetProperty("surface").GetString());
        Assert.Equal("space-grotesk", kit.GetProperty("headingFont").GetString());
        Assert.Equal(20, kit.GetProperty("radius").GetProperty("large").GetInt32());

        var fetched = await Json(await session.GetAsync($"/api/v1/brand-kits/{kit.GetProperty("id").GetString()}"));
        Assert.Equal("Warm, direct, no emojis.", fetched.GetProperty("voice").GetString());
    }

    [Fact]
    public async Task Setting_a_default_moves_it_and_list_puts_it_first()
    {
        var session = await RegisteredSessionAsync();
        var first = await CreateAsync(session, Kit("First"));
        var second = await CreateAsync(session, Kit("Second"));
        Assert.False(second.GetProperty("isDefault").GetBoolean());

        var secondId = second.GetProperty("id").GetString();
        var response = await session.PostAsync($"/api/v1/brand-kits/{secondId}/default");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await Json(await session.GetAsync("/api/v1/brand-kits"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(secondId, items[0].GetProperty("id").GetString());
        Assert.Single(items, i => i.GetProperty("isDefault").GetBoolean());
        Assert.Contains(items, i => i.GetProperty("id").GetString() == first.GetProperty("id").GetString()
            && !i.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task Deleting_the_default_promotes_the_newest_remaining_kit()
    {
        var session = await RegisteredSessionAsync();
        var first = await CreateAsync(session, Kit("First"));
        await CreateAsync(session, Kit("Second"));
        var third = await CreateAsync(session, Kit("Third"));

        var delete = await session.SendAsync(HttpMethod.Delete, $"/api/v1/brand-kits/{first.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var items = (await Json(await session.GetAsync("/api/v1/brand-kits"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var defaultKit = Assert.Single(items, i => i.GetProperty("isDefault").GetBoolean());
        Assert.Equal(third.GetProperty("id").GetString(), defaultKit.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Update_replaces_the_kit()
    {
        var session = await RegisteredSessionAsync();
        var kit = await CreateAsync(session, Kit("Acme"));

        var response = await session.SendAsync(HttpMethod.Put,
            $"/api/v1/brand-kits/{kit.GetProperty("id").GetString()}", Kit("Acme 2", primary: "#FF5500"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Json(response);
        Assert.Equal("Acme 2", updated.GetProperty("name").GetString());
        Assert.Equal("#FF5500", updated.GetProperty("colors").GetProperty("primary").GetString());
        Assert.True(updated.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task Invalid_colors_and_fonts_return_422()
    {
        var session = await RegisteredSessionAsync();

        var badColor = await session.PostAsync("/api/v1/brand-kits", Kit("Bad", primary: "red"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badColor.StatusCode);
        Assert.Equal("validation_failed", (await Json(badColor)).GetProperty("errorCode").GetString());

        var badFont = await session.PostAsync("/api/v1/brand-kits", new
        {
            name = "Bad font",
            colors = new { primary = "#000000", secondary = "#000000", background = "#000000", surface = "#000000",
                text = "#000000", muted = "#000000", link = "#000000" },
            headingFont = "comic-sans",
            bodyFont = "inter",
            button = new { radius = 4, style = "filled" },
            radius = new { small = 4, medium = 8, large = 12 },
            spacing = new { small = 4, medium = 8, large = 12 },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badFont.StatusCode);
    }

    [Fact]
    public async Task Kits_are_invisible_to_other_users()
    {
        var owner = await RegisteredSessionAsync();
        var kit = await CreateAsync(owner, Kit("Private"));
        var id = kit.GetProperty("id").GetString();

        var intruder = await RegisteredSessionAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/brand-kits/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.SendAsync(HttpMethod.Put, $"/api/v1/brand-kits/{id}", Kit("Mine"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.SendAsync(HttpMethod.Delete, $"/api/v1/brand-kits/{id}")).StatusCode);
        Assert.Empty((await Json(await intruder.GetAsync("/api/v1/brand-kits"))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task A_kit_limit_applies_per_user()
    {
        var session = await RegisteredSessionAsync();
        for (var i = 0; i < 10; i++) await CreateAsync(session, Kit($"Kit {i}"));

        var eleventh = await session.PostAsync("/api/v1/brand-kits", Kit("One too many"));

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        Assert.Equal("brand_kit.limit", (await Json(eleventh)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Logo_must_be_an_own_asset()
    {
        var session = await RegisteredSessionAsync();

        var response = await session.PostAsync("/api/v1/brand-kits", Kit("Logo", logoAssetId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("brand_kit.invalid_logo", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Logo_is_published_and_exposed_as_a_url()
    {
        var session = await RegisteredSessionAsync();
        var assetId = await UploadPngAsync(session);

        var kit = await CreateAsync(session, Kit("Logo", logoAssetId: assetId));

        Assert.Equal(assetId.ToString(), kit.GetProperty("logoAssetId").GetString());
        var logoUrl = kit.GetProperty("logoUrl").GetString();
        Assert.False(string.IsNullOrEmpty(logoUrl));

        var asset = await Json(await session.GetAsync($"/api/v1/assets/{assetId}"));
        Assert.Equal("public", asset.GetProperty("access").GetString(), ignoreCase: true);
        Assert.Equal(logoUrl, asset.GetProperty("publicUrl").GetString());
    }

    private static async Task<Guid> UploadPngAsync(TestSession session)
    {
        var grantResponse = await session.PostAsync("/api/v1/assets/uploads",
            new { filename = "logo.png", mimeType = "image/png", sizeBytes = TestFiles.Png.Length });
        grantResponse.EnsureSuccessStatusCode();
        var grant = await Json(grantResponse);
        var assetId = grant.GetProperty("assetId").GetString()!;

        using var http = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, grant.GetProperty("uploadUrl").GetString())
        {
            Content = new ByteArrayContent(TestFiles.Png),
        };
        put.Content.Headers.TryAddWithoutValidation("Content-Type", "image/png");
        (await http.SendAsync(put)).EnsureSuccessStatusCode();

        (await session.PostAsync($"/api/v1/assets/uploads/{assetId}/complete")).EnsureSuccessStatusCode();
        return Guid.Parse(assetId);
    }

    [Fact]
    public async Task Tokens_export_as_json_and_css()
    {
        var session = await RegisteredSessionAsync();
        var kit = await CreateAsync(session, Kit("Acme Blue"));
        var id = kit.GetProperty("id").GetString();

        var json = await session.GetAsync($"/api/v1/brand-kits/{id}/tokens");
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        Assert.Equal("acme-blue-tokens.json", json.Content.Headers.ContentDisposition?.FileNameStar
            ?? json.Content.Headers.ContentDisposition?.FileName);
        var tokens = await Json(json);
        Assert.Equal("#6C5CE7", tokens.GetProperty("colors").GetProperty("primary").GetString());
        Assert.Equal("16px", tokens.GetProperty("spacing").GetProperty("medium").GetString());
        Assert.Equal("Space Grotesk", tokens.GetProperty("typography").GetProperty("heading").GetProperty("family").GetString());

        var css = await session.GetAsync($"/api/v1/brand-kits/{id}/tokens?format=css");
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        var body = await css.Content.ReadAsStringAsync();
        Assert.Contains("--brand-color-primary: #6C5CE7;", body);
        Assert.Contains("--brand-radius-large: 20px;", body);

        var unknown = await session.GetAsync($"/api/v1/brand-kits/{id}/tokens?format=xml");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Font_list_is_available()
    {
        var session = await RegisteredSessionAsync();

        var fonts = (await Json(await session.GetAsync("/api/v1/brand-kits/fonts"))).GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(fonts, f => f.GetProperty("key").GetString() == "inter"
            && f.GetProperty("googleFamily").GetString() == "Inter");
        Assert.All(fonts, f => Assert.False(string.IsNullOrEmpty(f.GetProperty("stack").GetString())));
    }
}
