using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Infrastructure.Ai;
using MailTemplateHub.Infrastructure.Rendering;

namespace MailTemplateHub.UnitTests.Ai;

public class ScaffoldTemplateGeneratorTests
{
    private readonly ScaffoldTemplateGenerator _generator = new();

    private static AiTemplateRequest Request(string prompt = "A welcome email for new users", string? color = null,
        IReadOnlyList<string>? assets = null) =>
        new(prompt, color, Tone: "friendly", assets ?? [], []);

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
    public async Task Embeds_provided_asset_as_hero_image()
    {
        var url = "https://cdn.example/logo.png";
        var result = await _generator.GenerateAsync(Request(assets: [url]), CancellationToken.None);

        Assert.Contains(url, result.MjmlSource);
        Assert.Contains("mj-image", result.MjmlSource);
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
}
