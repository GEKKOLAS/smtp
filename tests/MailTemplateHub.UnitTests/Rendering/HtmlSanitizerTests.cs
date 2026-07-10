using MailTemplateHub.Infrastructure.Rendering;

namespace MailTemplateHub.UnitTests.Rendering;

public class HtmlSanitizerTests
{
    private readonly GanssHtmlSanitizer _sanitizer = new();

    [Theory]
    [InlineData("<script>alert('x')</script><p>hi</p>")]
    [InlineData("<img src=x onerror=\"alert(1)\">")]
    [InlineData("<a href=\"javascript:alert(1)\">click</a>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    [InlineData("<div onclick=\"steal()\">x</div>")]
    [InlineData("<form action=\"https://evil\"><input></form>")]
    [InlineData("<object data=\"evil.swf\"></object>")]
    public void Strips_dangerous_content(string dirty)
    {
        var clean = _sanitizer.Sanitize(dirty);

        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Keeps_email_safe_markup()
    {
        var html = """
            <table><tr><td style="padding:10px;color:#333">
            <a href="https://example.com">Visit</a>
            <img src="https://example.com/logo.png" alt="Logo" width="200">
            </td></tr></table>
            """;

        var clean = _sanitizer.Sanitize(html);

        Assert.Contains("<table", clean);
        Assert.Contains("href=\"https://example.com\"", clean);
        Assert.Contains("<img", clean);
        Assert.Contains("alt=\"Logo\"", clean);
        Assert.Contains("padding", clean);
    }

    [Fact]
    public void Preserves_mso_conditional_comments()
    {
        var html = "<!--[if mso]><table><tr><td>Outlook</td></tr></table><![endif]--><p>Body</p>";

        var clean = _sanitizer.Sanitize(html);

        Assert.Contains("[if mso]", clean);
    }

    [Fact]
    public void Allows_asset_marker_scheme()
    {
        var clean = _sanitizer.Sanitize("<img src=\"mth-asset://019f0000-0000-0000-0000-000000000000\" alt=\"x\">");
        Assert.Contains("mth-asset://", clean);
    }

    [Fact]
    public void Keeps_background_image_attribute_from_mjml_hero_sections()
    {
        // MJML compiles an mj-section background-url into a "background" HTML
        // attribute on a <table> (plus a VML fallback for Outlook), not a CSS
        // property — this must survive sanitization for hero/video-card sections.
        var html = """<table background="https://example.com/hero.jpg" role="presentation"><tr><td>Hero</td></tr></table>""";

        var clean = _sanitizer.Sanitize(html);

        Assert.Contains("https://example.com/hero.jpg", clean);
    }

    [Fact]
    public void Keeps_gradient_hero_styling_declared_via_mj_style_class()
    {
        // MJML has no gradient attribute on mj-section, so the AI is instructed to
        // emit a <style> class (mj-style) with a linear-gradient background-image
        // and apply it via css-class. Both the stylesheet rule and the class
        // reference must survive sanitization.
        var html = """
            <style>.gradient-hero { background-image: linear-gradient(135deg, #2563eb, #1d4ed8) !important; }</style>
            <div class="gradient-hero">Hero</div>
            """;

        var clean = _sanitizer.Sanitize(html);

        Assert.Contains("linear-gradient", clean);
        Assert.Contains("gradient-hero", clean);
    }
}
