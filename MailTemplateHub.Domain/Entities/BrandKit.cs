using MailTemplateHub.Domain.Common;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// A user's brand: palette, fonts, logo, button style, design-token scales and
/// tone of voice (spec 14 F1). Templates copy literal values out of a kit when a
/// brand block is inserted, so editing a kit never restyles sent history.
/// </summary>
public sealed class BrandKit : IHasTimestamps
{
    public const int MaxPerUser = 10;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Name { get; set; }

    /// <summary>Exactly one kit per user is the default once any exist.</summary>
    public bool IsDefault { get; set; }

    public required BrandColors Colors { get; set; }

    /// <summary>Keys from <see cref="BrandFonts"/>.</summary>
    public required string HeadingFont { get; set; }
    public required string BodyFont { get; set; }

    public Guid? LogoAssetId { get; set; }
    public Asset? LogoAsset { get; set; }

    public required BrandButtonStyle Button { get; set; }
    public required BrandScale Radius { get; set; }
    public required BrandScale Spacing { get; set; }

    /// <summary>Tone of voice, fed to AI generation and copy rewriting.</summary>
    public string? Voice { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Brand palette; every value is a #RRGGBB hex color.</summary>
public sealed class BrandColors
{
    public required string Primary { get; set; }
    public required string Secondary { get; set; }
    public required string Background { get; set; }
    public required string Surface { get; set; }
    public required string Text { get; set; }
    public required string Muted { get; set; }
    public required string Link { get; set; }
}

public sealed class BrandButtonStyle
{
    public const string Filled = "filled";
    public const string Outline = "outline";

    /// <summary>Corner radius in px.</summary>
    public required int Radius { get; set; }

    /// <summary><see cref="Filled"/> or <see cref="Outline"/>.</summary>
    public required string Style { get; set; }
}

/// <summary>A small/medium/large token scale in px (radius, spacing).</summary>
public sealed class BrandScale
{
    public required int Small { get; set; }
    public required int Medium { get; set; }
    public required int Large { get; set; }
}

/// <summary>
/// The fonts a kit may use. Every entry carries an email-safe fallback stack;
/// web fonts additionally name their Google Fonts family, which clients that
/// can't load web fonts (Outlook desktop, many Gmail apps) simply ignore.
/// </summary>
public static class BrandFonts
{
    public sealed record Font(string Key, string Label, string Stack, string? GoogleFamily);

    public static readonly IReadOnlyList<Font> All =
    [
        new("arial", "Arial", "Arial, Helvetica, sans-serif", null),
        new("helvetica", "Helvetica", "Helvetica, Arial, sans-serif", null),
        new("verdana", "Verdana", "Verdana, Geneva, sans-serif", null),
        new("tahoma", "Tahoma", "Tahoma, Verdana, sans-serif", null),
        new("trebuchet", "Trebuchet MS", "'Trebuchet MS', Arial, sans-serif", null),
        new("georgia", "Georgia", "Georgia, 'Times New Roman', serif", null),
        new("times", "Times New Roman", "'Times New Roman', Times, serif", null),
        new("courier", "Courier New", "'Courier New', Courier, monospace", null),
        new("inter", "Inter", "Inter, Arial, Helvetica, sans-serif", "Inter"),
        new("space-grotesk", "Space Grotesk", "'Space Grotesk', Arial, Helvetica, sans-serif", "Space Grotesk"),
        new("poppins", "Poppins", "Poppins, Arial, Helvetica, sans-serif", "Poppins"),
        new("montserrat", "Montserrat", "Montserrat, Arial, Helvetica, sans-serif", "Montserrat"),
        new("roboto", "Roboto", "Roboto, Arial, Helvetica, sans-serif", "Roboto"),
        new("lato", "Lato", "Lato, Arial, Helvetica, sans-serif", "Lato"),
        new("merriweather", "Merriweather", "Merriweather, Georgia, serif", "Merriweather"),
        new("playfair-display", "Playfair Display", "'Playfair Display', Georgia, serif", "Playfair Display"),
    ];

    public static Font? Find(string key) => All.FirstOrDefault(f => f.Key == key);
}
