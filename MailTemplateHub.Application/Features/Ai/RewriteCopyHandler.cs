using System.Text.RegularExpressions;
using FluentValidation;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Features.BrandKits;

namespace MailTemplateHub.Application.Features.Ai;

public sealed record RewriteCopyCommand(
    string Kind, string Text, string Tone, int? Count, Guid? BrandKitId, string? Context);

public sealed record CopyVariationsDto(IReadOnlyList<CopyVariation> Variations);

public sealed class RewriteCopyValidator : AbstractValidator<RewriteCopyCommand>
{
    public RewriteCopyValidator()
    {
        RuleFor(x => x.Kind).Must(k => CopyKinds.All.Contains(k))
            .WithMessage("Kind must be subject, preheader, body or cta.");
        RuleFor(x => x.Tone).Must(t => CopyTones.All.Contains(t))
            .WithMessage("Tone must be promotional, professional, technical or followUp.");
        RuleFor(x => x.Text).NotEmpty();
        RuleFor(x => x.Text.Length).LessThanOrEqualTo(x => CopyKinds.MaxLength(x.Kind))
            .When(x => x.Text is not null)
            .OverridePropertyName("text")
            .WithMessage(x => $"Text can be up to {CopyKinds.MaxLength(x.Kind)} characters.");
        RuleFor(x => x.Count).InclusiveBetween(1, 5).When(x => x.Count.HasValue);
        RuleFor(x => x.Context).MaximumLength(500);
    }
}

/// <summary>
/// Tone-based rewrites of subject lines, preheaders, text blocks and CTAs (spec 14
/// F2). The model's output is checked, not trusted: a variation that drops or
/// invents a {{placeholder}} would break personalization, so it is discarded.
/// </summary>
public sealed partial class RewriteCopyHandler(
    IAiCopyWriter writer,
    BrandKitsHandler brandKits,
    IValidator<RewriteCopyCommand> validator)
{
    private const int DefaultCount = 3;

    public async Task<CopyVariationsDto> HandleAsync(RewriteCopyCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        if (!writer.IsConfigured)
        {
            throw new AiUnavailableException("The copy assistant isn't configured — add an Anthropic API key.");
        }

        var voice = command.BrandKitId is { } kitId ? (await brandKits.FindForUseAsync(kitId, ct))?.Voice : null;
        var count = command.Count ?? DefaultCount;
        var original = command.Text.Trim();

        var variations = await writer.RewriteAsync(
            new CopyRequest(command.Kind, original, command.Tone, count, voice,
                string.IsNullOrWhiteSpace(command.Context) ? null : command.Context.Trim()),
            ct);

        var expected = Placeholders(original);
        var maxLength = CopyKinds.MaxLength(command.Kind);
        var kept = variations
            .Select(v => v with { Text = v.Text.Trim() })
            .Where(v => v.Text.Length > 0 && v.Text.Length <= maxLength)
            .Where(v => !string.Equals(v.Text, original, StringComparison.Ordinal))
            .Where(v => Placeholders(v.Text).SetEquals(expected))
            .DistinctBy(v => v.Text)
            .Take(count)
            .ToList();

        if (kept.Count == 0)
        {
            throw new AiGenerationException("The suggestions didn't keep your {{variables}} intact. Please try again.");
        }
        return new CopyVariationsDto(kept);
    }

    /// <summary>The {{placeholders}} in a text, normalized for whitespace.</summary>
    public static HashSet<string> Placeholders(string text) =>
        PlaceholderRegex().Matches(text)
            .Select(m => WhitespaceRegex().Replace(m.Groups[1].Value.Trim(), " "))
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"\{\{\{?([^{}]+?)\}?\}\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
