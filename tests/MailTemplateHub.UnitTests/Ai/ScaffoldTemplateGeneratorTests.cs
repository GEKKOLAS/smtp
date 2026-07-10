using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Infrastructure.Ai;
using MailTemplateHub.Infrastructure.Rendering;

namespace MailTemplateHub.UnitTests.Ai;

public class ScaffoldTemplateGeneratorTests
{
    private readonly ScaffoldTemplateGenerator _generator = new();

    private static AiTemplateRequest Request(string prompt = "A welcome email for new users", string? color = null,
        IReadOnlyList<string>? assets = null, string? videoUrl = null, string? videoThumbnailUrl = null) =>
        new(prompt, color, Tone: "friendly", assets ?? [], [], videoUrl, videoThumbnailUrl);

    [Fact]
    public async Task Produces_valid_compilable_mjml()
    {
        var result = await _generator.GenerateAsync(Request(), CancellationToken.None);

        Assert.False(_generator.IsRealAi);
        Assert.Contains("<mjml>", result.MjmlSource);
        Assert.Contains("{{firstName}}", result.MjmlSource);

        // The output must survive the real MJML compiler.
        var compiled = new MjmlNetCompiler().Compile(result.MjmlSource);
        Assert.True(compiled.Success, string.Join(",", compiled.Errors.Select(e => e.Message)));
        Assert.Contains("<table", compiled.Html);
    }

    [Fact]
    public async Task Embeds_provided_asset_as_hero_background()
    {
        var url = "https://cdn.example/logo.png";
        var result = await _generator.GenerateAsync(Request(assets: [url]), CancellationToken.None);

        Assert.Contains(url, result.MjmlSource);
        Assert.Contains("background-url", result.MjmlSource);

        // Must compile to a real MJML background (a "background" attribute on a
        // table, per real MJML output), not a dropped/invalid attribute.
        var compiled = new MjmlNetCompiler().Compile(result.MjmlSource);
        Assert.True(compiled.Success, string.Join(",", compiled.Errors.Select(e => e.Message)));
        Assert.Contains($"background=\"{url}\"", compiled.Html);
    }

    [Fact]
    public async Task Declares_variables_with_samples()
    {
        var result = await _generator.GenerateAsync(Request(), CancellationToken.None);

        Assert.Contains(result.Variables, v => v.Name == "firstName");
        Assert.Contains(result.Variables, v => v.Name == "ctaUrl" && v.Type == "url");
        Assert.All(result.Variables, v => Assert.False(string.IsNullOrEmpty(v.Sample)));
    }

    [Fact]
    public async Task Uses_brand_color_when_valid_hex()
    {
        var result = await _generator.GenerateAsync(Request(color: "#ff0000"), CancellationToken.None);
        Assert.Contains("#ff0000", result.MjmlSource);
    }

    [Fact]
    public async Task Escapes_prompt_content_in_body()
    {
        var result = await _generator.GenerateAsync(
            Request("Announce <script>alert(1)</script> our sale"), CancellationToken.None);
        // The prompt text is HTML-encoded before it lands in the MJML body.
        Assert.DoesNotContain("<script>alert(1)</script>", result.MjmlSource);
    }

    [Fact]
    public async Task Builds_video_thumbnail_card_with_play_link_never_a_video_tag()
    {
        var result = await _generator.GenerateAsync(
            Request(videoUrl: "https://youtu.be/dQw4w9WgXcQ",
                videoThumbnailUrl: "https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg"),
            CancellationToken.None);

        Assert.Contains("https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg", result.MjmlSource);
        Assert.Contains("https://youtu.be/dQw4w9WgXcQ", result.MjmlSource);
        Assert.DoesNotContain("<video", result.MjmlSource, StringComparison.OrdinalIgnoreCase);

        var compiled = new MjmlNetCompiler().Compile(result.MjmlSource);
        Assert.True(compiled.Success, string.Join(",", compiled.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task Falls_back_to_plain_watch_button_when_no_thumbnail_available()
    {
        var result = await _generator.GenerateAsync(
            Request(videoUrl: "https://vimeo.com/12345"), CancellationToken.None);

        Assert.Contains("https://vimeo.com/12345", result.MjmlSource);
        Assert.Contains("Watch the video", result.MjmlSource);
    }

    [Fact]
    public async Task Derives_a_preheader_distinct_from_the_subject()
    {
        var result = await _generator.GenerateAsync(
            Request("A welcome email. Get 10% off your first order today"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Preheader));
        Assert.NotEqual(result.Subject, result.Preheader);
    }
}
