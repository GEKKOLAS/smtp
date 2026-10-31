using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MailTemplateHub.Domain.Entities;

namespace MailTemplateHub.Application.Features.BrandKits;

public sealed record FontToken(string Family, string Stack, string? GoogleFamily);

public sealed record TypographyTokens(FontToken Heading, FontToken Body);

public sealed record ButtonTokens(string Radius, string Style);

/// <summary>
/// A brand kit as design tokens (spec 14 F1), in the shape of the toolkit's
/// design-system example: colors, spacing, radius, plus typography and button.
/// </summary>
public sealed record DesignTokens(
    string Name,
    IReadOnlyDictionary<string, string> Colors,
    TypographyTokens Typography,
    IReadOnlyDictionary<string, string> Spacing,
    IReadOnlyDictionary<string, string> Radius,
    ButtonTokens Button,
    string? Logo);

public static partial class BrandTokens
{
    public static DesignTokens From(BrandKit kit) => new(
        kit.Name,
        new Dictionary<string, string>
        {
            ["primary"] = kit.Colors.Primary,
            ["secondary"] = kit.Colors.Secondary,
            ["background"] = kit.Colors.Background,
            ["surface"] = kit.Colors.Surface,
            ["text"] = kit.Colors.Text,
            ["muted"] = kit.Colors.Muted,
            ["link"] = kit.Colors.Link,
        },
        new TypographyTokens(Font(kit.HeadingFont), Font(kit.BodyFont)),
        Scale(kit.Spacing),
        Scale(kit.Radius),
        new ButtonTokens(Px(kit.Button.Radius), kit.Button.Style),
        kit.LogoAsset?.PublicUrl);

    /// <summary>The same tokens as CSS custom properties on :root.</summary>
    public static string ToCss(DesignTokens tokens)
    {
        var css = new StringBuilder();
        // The name is user text inside a comment: drop anything that could close it.
        css.Append("/* Brand tokens: ").Append(tokens.Name.Replace("*/", string.Empty)).Append(" */\n");
        css.Append(":root {\n");
        foreach (var (name, value) in tokens.Colors) Property(css, $"color-{name}", value);
        Property(css, "font-heading", tokens.Typography.Heading.Stack);
        Property(css, "font-body", tokens.Typography.Body.Stack);
        foreach (var (name, value) in tokens.Spacing) Property(css, $"spacing-{name}", value);
        foreach (var (name, value) in tokens.Radius) Property(css, $"radius-{name}", value);
        Property(css, "button-radius", tokens.Button.Radius);
        css.Append("}\n");
        return css.ToString();
    }

    /// <summary>A filename-safe slug of the kit name, e.g. "Acme Blue" → "acme-blue".</summary>
    public static string Slug(string name)
    {
        var slug = NonSlugCharacters().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "brand" : slug;
    }

    private static FontToken Font(string key)
    {
        var font = BrandFonts.Find(key) ?? BrandFonts.All[0];
        return new FontToken(font.Label, font.Stack, font.GoogleFamily);
    }

    private static Dictionary<string, string> Scale(BrandScale scale) => new()
    {
        ["small"] = Px(scale.Small),
        ["medium"] = Px(scale.Medium),
        ["large"] = Px(scale.Large),
    };

    private static string Px(int value) => value.ToString(CultureInfo.InvariantCulture) + "px";

    private static void Property(StringBuilder css, string name, string value) =>
        css.Append("  --brand-").Append(name).Append(": ").Append(value).Append(";\n");

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
