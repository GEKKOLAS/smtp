using System.Net;
using System.Text;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.UnitTests.Ai;

public class OpenAiImageGeneratorTests
{
    // A real 1x1 PNG (valid magic bytes + IHDR).
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private static OpenAiImageGenerator Generator(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new StubHandler(status, body));
        var options = Options.Create(new ImageGenerationOptions { ApiKey = "sk-test" });
        return new OpenAiImageGenerator(http, options);
    }

    [Fact]
    public async Task Decodes_b64_json_png_bytes()
    {
        var json = $$"""{"data":[{"b64_json":"{{OnePixelPngBase64}}"}]}""";
        var generator = Generator(HttpStatusCode.OK, json);

        var result = await generator.GenerateAsync("a red circle", "1024x1024", CancellationToken.None);

        Assert.Equal("image/png", result.MimeType);
        Assert.Equal(Convert.FromBase64String(OnePixelPngBase64), result.Bytes);
        Assert.True(result.Bytes.Length > 0);
    }

    [Fact]
    public async Task Throws_on_non_success_status()
    {
        var generator = Generator(HttpStatusCode.BadRequest, """{"error":{"message":"bad"}}""");

        await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateAsync("x", null, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_response_has_no_image()
    {
        var generator = Generator(HttpStatusCode.OK, """{"data":[]}""");

        await Assert.ThrowsAsync<AiGenerationException>(() =>
            generator.GenerateAsync("x", null, CancellationToken.None));
    }

    [Fact]
    public async Task Disabled_generator_reports_not_configured_and_throws()
    {
        var disabled = new DisabledImageGenerator();

        Assert.False(disabled.IsConfigured);
        await Assert.ThrowsAsync<AiGenerationException>(() =>
            disabled.GenerateAsync("x", null, CancellationToken.None));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
