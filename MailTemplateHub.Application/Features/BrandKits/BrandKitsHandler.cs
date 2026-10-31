using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Assets;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.BrandKits;

public sealed record BrandColorsDto(
    string Primary, string Secondary, string Background, string Surface, string Text, string Muted, string Link);

public sealed record BrandButtonDto(int Radius, string Style);

public sealed record BrandScaleDto(int Small, int Medium, int Large);

public sealed record SaveBrandKitCommand(
    string Name,
    BrandColorsDto Colors,
    string HeadingFont,
    string BodyFont,
    Guid? LogoAssetId,
    BrandButtonDto Button,
    BrandScaleDto Radius,
    BrandScaleDto Spacing,
    string? Voice);

public sealed record BrandKitDto(
    Guid Id,
    string Name,
    bool IsDefault,
    BrandColorsDto Colors,
    string HeadingFont,
    string BodyFont,
    Guid? LogoAssetId,
    string? LogoUrl,
    BrandButtonDto Button,
    BrandScaleDto Radius,
    BrandScaleDto Spacing,
    string? Voice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static BrandKitDto From(BrandKit k) => new(
        k.Id, k.Name, k.IsDefault,
        new BrandColorsDto(k.Colors.Primary, k.Colors.Secondary, k.Colors.Background, k.Colors.Surface,
            k.Colors.Text, k.Colors.Muted, k.Colors.Link),
        k.HeadingFont, k.BodyFont,
        k.LogoAsset is null ? null : k.LogoAssetId,
        k.LogoAsset?.PublicUrl,
        new BrandButtonDto(k.Button.Radius, k.Button.Style),
        new BrandScaleDto(k.Radius.Small, k.Radius.Medium, k.Radius.Large),
        new BrandScaleDto(k.Spacing.Small, k.Spacing.Medium, k.Spacing.Large),
        k.Voice, k.CreatedAt, k.UpdatedAt);
}

public sealed class SaveBrandKitValidator : AbstractValidator<SaveBrandKitCommand>
{
    private const string HexColor = "^#[0-9A-Fa-f]{6}$";

    public SaveBrandKitValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.HeadingFont).Must(f => BrandFonts.Find(f) is not null).WithMessage("Unknown font.");
        RuleFor(x => x.BodyFont).Must(f => BrandFonts.Find(f) is not null).WithMessage("Unknown font.");
        RuleFor(x => x.Voice).MaximumLength(500);

        RuleFor(x => x.Colors).NotNull().ChildRules(colors =>
        {
            colors.RuleFor(c => c.Primary).Matches(HexColor);
            colors.RuleFor(c => c.Secondary).Matches(HexColor);
            colors.RuleFor(c => c.Background).Matches(HexColor);
            colors.RuleFor(c => c.Surface).Matches(HexColor);
            colors.RuleFor(c => c.Text).Matches(HexColor);
            colors.RuleFor(c => c.Muted).Matches(HexColor);
            colors.RuleFor(c => c.Link).Matches(HexColor);
        });

        RuleFor(x => x.Button).NotNull().ChildRules(button =>
        {
            button.RuleFor(b => b.Radius).InclusiveBetween(0, 32);
            button.RuleFor(b => b.Style).Must(s => s is BrandButtonStyle.Filled or BrandButtonStyle.Outline)
                .WithMessage("Style must be 'filled' or 'outline'.");
        });

        RuleFor(x => x.Radius).NotNull().ChildRules(scale => ScaleRules(scale, 40));
        RuleFor(x => x.Spacing).NotNull().ChildRules(scale => ScaleRules(scale, 64));
    }

    private static void ScaleRules(InlineValidator<BrandScaleDto> scale, int max)
    {
        scale.RuleFor(s => s.Small).InclusiveBetween(0, max);
        scale.RuleFor(s => s.Medium).InclusiveBetween(0, max);
        scale.RuleFor(s => s.Large).InclusiveBetween(0, max);
    }
}

/// <summary>Brand kit CRUD, default selection and design-token export (spec 14 F1).</summary>
public sealed class BrandKitsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    AssetsHandler assets,
    IAuditWriter audit,
    IValidator<SaveBrandKitCommand> validator)
{
    public async Task<IReadOnlyList<BrandKitDto>> ListAsync(CancellationToken ct)
    {
        var kits = await OwnKits()
            .OrderByDescending(k => k.IsDefault)
            .ThenBy(k => k.CreatedAt)
            .ToListAsync(ct);
        return kits.Select(BrandKitDto.From).ToList();
    }

    public async Task<BrandKitDto> GetAsync(Guid id, CancellationToken ct) =>
        BrandKitDto.From(await FindAsync(id, ct));

    /// <summary>The kit to brand new content with, or null when the user has none.</summary>
    public async Task<BrandKit?> FindForUseAsync(Guid? id, CancellationToken ct) =>
        id is { } kitId
            ? await FindAsync(kitId, ct)
            : await OwnKits().OrderByDescending(k => k.IsDefault).ThenByDescending(k => k.CreatedAt)
                .FirstOrDefaultAsync(ct);

    public async Task<BrandKitDto> CreateAsync(SaveBrandKitCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var count = await OwnKits().CountAsync(ct);
        if (count >= BrandKit.MaxPerUser)
        {
            throw new ConflictException("brand_kit.limit", $"You can keep up to {BrandKit.MaxPerUser} brand kits.");
        }

        var kit = new BrandKit
        {
            UserId = currentUser.UserId,
            Name = command.Name.Trim(),
            IsDefault = count == 0,
            Colors = null!,
            HeadingFont = command.HeadingFont,
            BodyFont = command.BodyFont,
            Button = null!,
            Radius = null!,
            Spacing = null!,
        };
        await ApplyAsync(kit, command, ct);
        db.BrandKits.Add(kit);
        audit.Add(AuditActions.BrandKitCreated, currentUser.UserId, "brand_kit", kit.Id, new { kit.Name });
        await db.SaveChangesAsync(ct);

        return BrandKitDto.From(kit);
    }

    public async Task<BrandKitDto> UpdateAsync(Guid id, SaveBrandKitCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        var kit = await FindAsync(id, ct);

        await ApplyAsync(kit, command, ct);
        audit.Add(AuditActions.BrandKitUpdated, currentUser.UserId, "brand_kit", kit.Id);
        await db.SaveChangesAsync(ct);

        return BrandKitDto.From(kit);
    }

    public async Task<BrandKitDto> SetDefaultAsync(Guid id, CancellationToken ct)
    {
        var kit = await FindAsync(id, ct);
        if (kit.IsDefault) return BrandKitDto.From(kit);

        // Clear first: the partial unique index allows one default per user, and
        // Postgres checks it per statement.
        await db.BrandKits.Where(k => k.UserId == currentUser.UserId && k.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.IsDefault, false), ct);
        kit.IsDefault = true;
        audit.Add(AuditActions.BrandKitDefaultChanged, currentUser.UserId, "brand_kit", kit.Id);
        await db.SaveChangesAsync(ct);

        return BrandKitDto.From(kit);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var kit = await FindAsync(id, ct);
        db.BrandKits.Remove(kit);
        audit.Add(AuditActions.BrandKitDeleted, currentUser.UserId, "brand_kit", kit.Id, new { kit.Name });
        await db.SaveChangesAsync(ct);

        if (!kit.IsDefault) return;

        // Keep exactly one default while any kit is left: promote the newest.
        var next = await OwnKits().OrderByDescending(k => k.CreatedAt).FirstOrDefaultAsync(ct);
        if (next is null) return;
        next.IsDefault = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DesignTokens> GetTokensAsync(Guid id, CancellationToken ct) =>
        BrandTokens.From(await FindAsync(id, ct));

    private async Task ApplyAsync(BrandKit kit, SaveBrandKitCommand command, CancellationToken ct)
    {
        kit.Name = command.Name.Trim();
        kit.Colors = new BrandColors
        {
            Primary = command.Colors.Primary.ToUpperInvariant(),
            Secondary = command.Colors.Secondary.ToUpperInvariant(),
            Background = command.Colors.Background.ToUpperInvariant(),
            Surface = command.Colors.Surface.ToUpperInvariant(),
            Text = command.Colors.Text.ToUpperInvariant(),
            Muted = command.Colors.Muted.ToUpperInvariant(),
            Link = command.Colors.Link.ToUpperInvariant(),
        };
        kit.HeadingFont = command.HeadingFont;
        kit.BodyFont = command.BodyFont;
        kit.Button = new BrandButtonStyle { Radius = command.Button.Radius, Style = command.Button.Style };
        kit.Radius = new BrandScale { Small = command.Radius.Small, Medium = command.Radius.Medium, Large = command.Radius.Large };
        kit.Spacing = new BrandScale { Small = command.Spacing.Small, Medium = command.Spacing.Medium, Large = command.Spacing.Large };
        kit.Voice = string.IsNullOrWhiteSpace(command.Voice) ? null : command.Voice.Trim();

        if (command.LogoAssetId is not { } logoId)
        {
            kit.LogoAssetId = null;
            kit.LogoAsset = null;
            return;
        }

        var logo = await db.Assets.FirstOrDefaultAsync(
            a => a.Id == logoId && a.UserId == currentUser.UserId && a.UploadState == AssetUploadState.Ready, ct);
        if (logo is null || !AllowedFileTypes.IsImageKind(logo.Kind))
        {
            throw new ConflictException("brand_kit.invalid_logo", "The logo must be one of your images.");
        }

        // Emails reference the logo by URL, so it has to be served publicly.
        if (logo.Access != AssetAccess.Public)
        {
            await assets.SetVisibilityAsync(logo.Id, AssetAccess.Public, ct);
        }
        kit.LogoAssetId = logo.Id;
        kit.LogoAsset = logo;
    }

    private IQueryable<BrandKit> OwnKits() =>
        db.BrandKits.Include(k => k.LogoAsset).Where(k => k.UserId == currentUser.UserId);

    private async Task<BrandKit> FindAsync(Guid id, CancellationToken ct) =>
        await OwnKits().FirstOrDefaultAsync(k => k.Id == id, ct) ?? throw new NotFoundException();
}
