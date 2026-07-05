using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailTemplateHub.IntegrationTests;

public class AssetsTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>Runs the full presign -> direct PUT -> complete flow; returns the completed asset JSON.</summary>
    private async Task<(HttpResponseMessage Complete, JsonElement Body)> UploadAsync(
        TestSession session, string filename, string mimeType, byte[] content)
    {
        var grantResponse = await session.PostAsync("/api/v1/assets/uploads",
            new { filename, mimeType, sizeBytes = content.Length });
        grantResponse.EnsureSuccessStatusCode();
        var grant = await grantResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assetId = grant.GetProperty("assetId").GetString()!;
        var uploadUrl = grant.GetProperty("uploadUrl").GetString()!;

        // Upload directly to MinIO via the presigned URL, as the browser would.
        using var http = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = new ByteArrayContent(content),
        };
        put.Content.Headers.Remove("Content-Type");
        put.Content.Headers.TryAddWithoutValidation("Content-Type", mimeType);
        var putResponse = await http.SendAsync(put);
        putResponse.EnsureSuccessStatusCode();

        var complete = await session.PostAsync($"/api/v1/assets/uploads/{assetId}/complete");
        var body = complete.StatusCode == HttpStatusCode.OK
            ? await complete.Content.ReadFromJsonAsync<JsonElement>()
            : default;
        return (complete, body);
    }

    [Fact]
    public async Task Upload_png_end_to_end_lists_ready_asset_with_dimensions()
    {
        var session = await RegisteredSessionAsync();

        var (complete, asset) = await UploadAsync(session, "logo.png", "image/png", TestFiles.Png);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("image", asset.GetProperty("kind").GetString());
        Assert.Equal(1, asset.GetProperty("width").GetInt32());
        Assert.Equal(1, asset.GetProperty("height").GetInt32());
        Assert.Equal("private", asset.GetProperty("access").GetString());

        var list = await session.GetAsync("/api/v1/assets");
        var items = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
    }

    [Fact]
    public async Task Upload_gif_is_classified_as_gif_kind()
    {
        var session = await RegisteredSessionAsync();
        var (complete, asset) = await UploadAsync(session, "loop.gif", "image/gif", TestFiles.Gif);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("gif", asset.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Content_not_matching_declared_type_is_rejected()
    {
        var session = await RegisteredSessionAsync();
        // PDF bytes uploaded as image/png -> magic-byte mismatch.
        var (complete, _) = await UploadAsync(session, "fake.png", "image/png", TestFiles.Pdf);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, complete.StatusCode);
        var body = await complete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("asset.verification_failed", body.GetProperty("errorCode").GetString());

        // Rejected uploads never appear in the library.
        var list = await session.GetAsync("/api/v1/assets");
        Assert.Equal(0, (await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Disallowed_mime_type_is_rejected_at_request_time()
    {
        var session = await RegisteredSessionAsync();
        var response = await session.PostAsync("/api/v1/assets/uploads",
            new { filename = "evil.svg", mimeType = "image/svg+xml", sizeBytes = 100 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Identical_files_dedupe_to_one_asset()
    {
        var session = await RegisteredSessionAsync();
        var (first, firstAsset) = await UploadAsync(session, "a.png", "image/png", TestFiles.Png);
        var (second, secondAsset) = await UploadAsync(session, "b.png", "image/png", TestFiles.Png);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // Same checksum resolves to the original asset id.
        Assert.Equal(firstAsset.GetProperty("id").GetString(), secondAsset.GetProperty("id").GetString());

        var list = await session.GetAsync("/api/v1/assets");
        Assert.Equal(1, (await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Make_image_public_exposes_a_public_url()
    {
        var session = await RegisteredSessionAsync();
        var (_, asset) = await UploadAsync(session, "hero.png", "image/png", TestFiles.Png);
        var id = asset.GetProperty("id").GetString();

        var response = await session.PostAsync($"/api/v1/assets/{id}/visibility", new { access = "public" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("public", updated.GetProperty("access").GetString());
        Assert.False(string.IsNullOrEmpty(updated.GetProperty("publicUrl").GetString()));

        // download-url of a public asset returns the public URL.
        var dl = await session.GetAsync($"/api/v1/assets/{id}/download-url");
        var url = (await dl.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString();
        Assert.Contains("mth-public", url);
    }

    [Fact]
    public async Task Non_image_cannot_be_made_public()
    {
        var session = await RegisteredSessionAsync();
        var (_, asset) = await UploadAsync(session, "doc.pdf", "application/pdf", TestFiles.Pdf);
        var id = asset.GetProperty("id").GetString();

        var response = await session.PostAsync($"/api/v1/assets/{id}/visibility", new { access = "public" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Private_asset_download_url_is_presigned()
    {
        var session = await RegisteredSessionAsync();
        var (_, asset) = await UploadAsync(session, "secret.png", "image/png", TestFiles.Png);
        var id = asset.GetProperty("id").GetString();

        var dl = await session.GetAsync($"/api/v1/assets/{id}/download-url");
        var url = (await dl.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;

        // The presigned URL is actually fetchable and returns the original bytes.
        using var http = new HttpClient();
        var fetched = await http.GetByteArrayAsync(url);
        Assert.Equal(TestFiles.Png, fetched);
    }

    [Fact]
    public async Task Delete_removes_asset_from_library()
    {
        var session = await RegisteredSessionAsync();
        var (_, asset) = await UploadAsync(session, "temp.png", "image/png", TestFiles.Png);
        var id = asset.GetProperty("id").GetString();

        var delete = await session.SendAsync(HttpMethod.Delete, $"/api/v1/assets/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await session.GetAsync($"/api/v1/assets/{id}")).StatusCode);
    }

    [Fact]
    public async Task Assets_are_isolated_between_users()
    {
        var owner = await RegisteredSessionAsync();
        var (_, asset) = await UploadAsync(owner, "owned.png", "image/png", TestFiles.Png);
        var id = asset.GetProperty("id").GetString();

        var stranger = await RegisteredSessionAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/assets/{id}")).StatusCode);
        Assert.Equal(0, (await (await stranger.GetAsync("/api/v1/assets")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Asset_used_by_a_template_cannot_be_deleted_without_force()
    {
        var session = await RegisteredSessionAsync();
        var (_, asset) = await UploadAsync(session, "inuse.png", "image/png", TestFiles.Png);
        var assetId = asset.GetProperty("id").GetString()!;

        // Create a template referencing the asset as a hosted image.
        var content = new
        {
            editorKind = "html",
            subject = "s",
            preheader = (string?)null,
            mjmlSource = (string?)null,
            grapesProject = (object?)null,
            htmlBody = $"<img src=\"mth-asset://{assetId}\" alt=\"x\">",
            textBody = (string?)null,
            variables = Array.Empty<object>(),
            assets = new[] { new { assetId, usage = "hosted_image", contentId = (string?)null } },
        };
        var create = await session.PostAsync("/api/v1/templates",
            new { name = $"Uses asset {Guid.NewGuid():N}", description = (string?)null, content });
        create.EnsureSuccessStatusCode();

        // Delete is blocked with a usages list.
        var blocked = await session.SendAsync(HttpMethod.Delete, $"/api/v1/assets/{assetId}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("asset.in_use", body.GetProperty("errorCode").GetString());
        Assert.True(body.GetProperty("usages").GetArrayLength() > 0);

        // Force deletes it anyway.
        var forced = await session.SendAsync(HttpMethod.Delete, $"/api/v1/assets/{assetId}?force=true");
        Assert.Equal(HttpStatusCode.NoContent, forced.StatusCode);
    }

    [Fact]
    public async Task Search_and_kind_filters_work()
    {
        var session = await RegisteredSessionAsync();
        await UploadAsync(session, "invoice.pdf", "application/pdf", TestFiles.Pdf);
        await UploadAsync(session, "banner.png", "image/png", TestFiles.Png);

        var byKind = await session.GetAsync("/api/v1/assets?kind=image");
        Assert.Equal(1, (await byKind.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());

        var bySearch = await session.GetAsync("/api/v1/assets?search=invoice");
        var items = (await bySearch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("invoice.pdf", items[0].GetProperty("originalFilename").GetString());
    }
}
