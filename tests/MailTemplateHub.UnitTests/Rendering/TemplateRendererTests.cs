using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Domain.Enums;
using MailTemplateHub.Infrastructure.Rendering;

namespace MailTemplateHub.UnitTests.Rendering;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new(new MjmlNetCompiler(), new GanssHtmlSanitizer());

    private static TemplateContent Content(
        string subject = "Hello {{firstName}}",
        string? mjml = null,
        string html = "<p>Hi {{firstName}}</p>",
        EditorKind kind = EditorKind.Html,
        IReadOnlyList<TemplateVariable>? variables = null,
        string? textBody = null) =>
        new(subject, "Preheader for {{firstName}}", kind, mjml, html, textBody,
            variables ?? [new TemplateVariable("firstName", TemplateVariableType.Text, true, null, "Ada")],
            []);

    private static RenderRequest Request(
        TemplateContent content, bool strict = false,
        IReadOnlyDictionary<string, string?>? vars = null,
        IReadOnlyDictionary<Guid, string>? assets = null) =>
        new(content, vars ?? new Dictionary<string, string?> { ["firstName"] = "Ada" },
            strict, assets ?? new Dictionary<Guid, string>());

    [Fact]
    public void Renders_variables_into_subject_and_body()
    {
        var result = _renderer.Render(Request(Content()));

        Assert.Equal("Hello Ada", result.Subject);
        Assert.Contains("Hi Ada", result.Html);
        Assert.Contains("Ada", result.Text);
    }

    [Fact]
    public void Html_encodes_text_variable_values()
    {
        var result = _renderer.Render(Request(Content(),
            vars: new Dictionary<string, string?> { ["firstName"] = "<b>Ada</b>" }));

        // The injected value is encoded, not interpreted as markup.
        Assert.Contains("&lt;b&gt;Ada&lt;/b&gt;", result.Html);
        Assert.DoesNotContain("<b>Ada</b>", result.Html);
    }

    [Fact]
    public void Compiles_mjml_to_responsive_html()
    {
        var mjml = "<mjml><mj-body><mj-section><mj-column><mj-text>Hi {{firstName}}</mj-text></mj-column></mj-section></mj-body></mjml>";
        var result = _renderer.Render(Request(Content(mjml: mjml, kind: EditorKind.Mjml)));

        Assert.Contains("Hi Ada", result.Html);
        Assert.Contains("<table", result.Html); // MJML emits table-based layout
    }

    [Fact]
    public void Strict_mode_throws_on_missing_required_variable()
    {
        var ex = Assert.Throws<MissingVariablesException>(() =>
            _renderer.Render(Request(Content(), strict: true,
                vars: new Dictionary<string, string?>())));

        Assert.Contains("firstName", ex.Missing);
    }

    [Fact]
    public void Sample_mode_warns_but_does_not_throw_on_missing_variable()
    {
        var result = _renderer.Render(Request(Content(), strict: false,
            vars: new Dictionary<string, string?>()));

        Assert.Contains(result.Warnings, w => w.Code == "variable.unfilled");
    }

    [Fact]
    public void Invalid_mjml_throws_with_positions()
    {
        var content = Content(mjml: "<mjml><mj-body><mj-not-a-tag></mj-not-a-tag></mj-body></mjml>", kind: EditorKind.Mjml);
        Assert.Throws<MjmlInvalidException>(() => _renderer.Render(Request(content)));
    }

    [Fact]
    public void Resolves_asset_markers_to_urls()
    {
        var assetId = Guid.CreateVersion7();
        var content = Content(html: $"<img src=\"mth-asset://{assetId}\" alt=\"logo\">");
        var result = _renderer.Render(Request(content,
            assets: new Dictionary<Guid, string> { [assetId] = "https://cdn.example/logo.png" }));

        Assert.Contains("https://cdn.example/logo.png", result.Html);
        Assert.DoesNotContain("mth-asset://", result.Html);
    }

    [Fact]
    public void Generates_plain_text_when_none_supplied()
    {
        var content = Content(html: "<h1>Title</h1><p>Visit <a href=\"https://x.example\">here</a></p>");
        var result = _renderer.Render(Request(content));

        Assert.Contains("Title", result.Text);
        Assert.Contains("https://x.example", result.Text);
        Assert.DoesNotContain("<h1>", result.Text);
    }

    [Fact]
    public void Warns_on_image_missing_alt()
    {
        var content = Content(html: "<img src=\"https://x.example/a.png\">");
        var result = _renderer.Render(Request(content));

        Assert.Contains(result.Warnings, w => w.Code == "image.missing_alt");
    }

    [Fact]
    public void Rendering_is_deterministic()
    {
        var first = _renderer.Render(Request(Content()));
        var second = _renderer.Render(Request(Content()));
        Assert.Equal(first.Html, second.Html);
    }
}
