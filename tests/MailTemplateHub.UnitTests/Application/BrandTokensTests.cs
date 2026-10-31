using MailTemplateHub.Application.Features.BrandKits;
using MailTemplateHub.Domain.Entities;

namespace MailTemplateHub.UnitTests.Application;

public class BrandTokensTests
{
    private static BrandKit Kit(string name = "Acme") => new()
    {
        UserId = Guid.NewGuid(),
        Name = name,
        Colors = new BrandColors
        {
            Primary = "#6C5CE7", Secondary = "#00CEC9", Background = "#101114", Surface = "#1B1D23",
            Text = "#F5F5F7", Muted = "#9A9AA5", Link = "#A29BFE",
        },
        HeadingFont = "playfair-display",
        BodyFont = "arial",
        Button = new BrandButtonStyle { Radius = 6, Style = BrandButtonStyle.Outline },
        Radius = new BrandScale { Small = 8, Medium = 12, Large = 20 },
        Spacing = new BrandScale { Small = 8, Medium = 16, Large = 24 },
    };

    [Fact]
    public void Tokens_follow_the_design_system_shape()
    {
        var tokens = BrandTokens.From(Kit());

        Assert.Equal("#6C5CE7", tokens.Colors["primary"]);
        Assert.Equal(["small", "medium", "large"], tokens.Spacing.Keys);
        Assert.Equal("24px", tokens.Spacing["large"]);
        Assert.Equal("12px", tokens.Radius["medium"]);
        Assert.Equal(new ButtonTokens("6px", "outline"), tokens.Button);
        Assert.Equal("Playfair Display", tokens.Typography.Heading.GoogleFamily);
        Assert.Null(tokens.Typography.Body.GoogleFamily);
        Assert.StartsWith("Arial", tokens.Typography.Body.Stack);
        Assert.Null(tokens.Logo);
    }

    [Fact]
    public void Css_emits_custom_properties()
    {
        var css = BrandTokens.ToCss(BrandTokens.From(Kit()));

        Assert.Contains(":root {", css);
        Assert.Contains("  --brand-color-surface: #1B1D23;\n", css);
        Assert.Contains("  --brand-font-heading: 'Playfair Display', Georgia, serif;\n", css);
        Assert.Contains("  --brand-spacing-small: 8px;\n", css);
        Assert.Contains("  --brand-button-radius: 6px;\n", css);
    }

    [Fact]
    public void Kit_name_cannot_close_the_css_comment()
    {
        var css = BrandTokens.ToCss(BrandTokens.From(Kit("Evil */ body { display: none } /*")));

        // Only the header comment's own opener and closer remain.
        Assert.Equal(1, css.Split("*/").Length - 1);
        Assert.StartsWith("/* Brand tokens: Evil  body { display: none } /* */\n:root {", css);
    }

    [Theory]
    [InlineData("Acme Blue", "acme-blue")]
    [InlineData("  Café & Co.  ", "caf-co")]
    [InlineData("***", "brand")]
    public void Slug_is_filename_safe(string name, string expected) =>
        Assert.Equal(expected, BrandTokens.Slug(name));
}
