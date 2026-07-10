using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Ai;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Ai;

public sealed record GenerateTemplateCommand(
    string Prompt,
    string? BrandColor,
    string? Tone,
    IReadOnlyList<Guid> AssetIds,
    IReadOnlyList<string> DesiredVariables,
    string? VideoUrl);

public sealed record GeneratedVariableDto(string Name, string Type, string Sample);

public sealed record GeneratedTemplateDto(
    string Subject,
    string? Preheader,
    string MjmlSource,
    string HtmlBody,
    IReadOnlyList<GeneratedVariableDto> Variables,
    string PreviewHtml,
    bool AiGenerated);

/// <summary>
/// Generates an MJML template from a prompt, compiles + renders it for preview,
/// and returns content ready to save via POST /templates. Stateless (no persist),
/// so it also serves the WhatsApp/n8n "generate -> approve -> send" flow.
/// </summary>
public sealed partial class GenerateTemplateHandler(
    IAiTemplateGenerator generator,
    IMjmlCompiler mjmlCompiler,
    ITemplateRenderer renderer,
    IAppDbContext db,
    ICurrentUser currentUser,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions)
{
    public async Task<GeneratedTemplateDto> HandleAsync(GenerateTemplateCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Prompt) || command.Prompt.Length > 2000)
        {
            throw new ValidationFailureLite("prompt", "A prompt of up to 2000 characters is required.");
        }

        var assetUrls = await ResolveAssetUrlsAsync(command.AssetIds, ct);
        var videoUrl = string.IsNullOrWhiteSpace(command.VideoUrl) ? null : command.VideoUrl.Trim();
        var videoThumbnailUrl = videoUrl is null ? null : DeriveVideoThumbnailUrl(videoUrl);

        var generated = await generator.GenerateAsync(
            new AiTemplateRequest(command.Prompt, command.BrandColor, command.Tone,
                assetUrls.Values.ToList(), command.DesiredVariables, videoUrl, videoThumbnailUrl),
            ct);

        // The generated MJML must compile; the scaffold always does, a real model
        // occasionally won't (surfaced as 422 so the caller can regenerate).
        var compiled = mjmlCompiler.Compile(generated.MjmlSource);
        if (!compiled.Success)
        {
            throw new AiGenerationException("The generated template was not valid. Please try again.");
        }

        var variables = generated.Variables
            .Select(v => new TemplateVariable(
                v.Name,
                Enum.TryParse<TemplateVariableType>(v.Type, ignoreCase: true, out var t) ? t : TemplateVariableType.Text,
                Required: false, Default: null, Sample: v.Sample))
            .ToList();

        var previewVars = variables.ToDictionary(v => v.Name, v => (string?)v.Sample);
        var content = new TemplateContent(
            generated.Subject, generated.Preheader, EditorKind.Mjml, generated.MjmlSource, compiled.Html, null,
            variables, []);
        var rendered = renderer.Render(new RenderRequest(content, previewVars, Strict: false,
            new Dictionary<Guid, string>()));

        return new GeneratedTemplateDto(
            generated.Subject,
            generated.Preheader,
            generated.MjmlSource,
            compiled.Html,
            generated.Variables.Select(v => new GeneratedVariableDto(v.Name, v.Type, v.Sample)).ToList(),
            rendered.Html,
            generator.IsRealAi);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveAssetUrlsAsync(
        IReadOnlyList<Guid> assetIds, CancellationToken ct)
    {
        var map = new Dictionary<Guid, string>();
        if (assetIds.Count == 0) return map;

        var options = storageOptions.Value;
        var owned = await db.Assets
            .Where(a => assetIds.Contains(a.Id)
                        && a.UserId == currentUser.UserId
                        && a.UploadState == AssetUploadState.Ready)
            .ToListAsync(ct);

        foreach (var asset in owned)
        {
            map[asset.Id] = asset is { Access: AssetAccess.Public, PublicUrl: { } url }
                ? url
                : await storage.CreatePresignedDownloadUrlAsync(
                    options.PrivateBucket, asset.StorageKey,
                    TimeSpan.FromMinutes(options.DownloadUrlExpiryMinutes), ct);
        }
        return map;
    }

    // Email clients don't render <video> or execute JavaScript, so a linked video
    // must ship as a static thumbnail with a play-button overlay. For YouTube URLs
    // the thumbnail is derivable from the video ID with no network call; other
    // hosts fall back to a plain "watch video" link (no fabricated thumbnail).
    private static string? DeriveVideoThumbnailUrl(string videoUrl)
    {
        var match = YouTubeIdRegex().Match(videoUrl);
        return match.Success ? $"https://img.youtube.com/vi/{match.Groups[1].Value}/hqdefault.jpg" : null;
    }

    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?v=|shorts/|embed/)|youtu\.be/)([A-Za-z0-9_-]{11})")]
    private static partial Regex YouTubeIdRegex();
}

/// <summary>Lightweight validation failure (maps to 422) for the AI feature.</summary>
public sealed class ValidationFailureLite(string field, string message) : AppException("validation_failed", message)
{
    public string Field { get; } = field;
}
