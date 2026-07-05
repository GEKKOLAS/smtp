using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Templates;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Rendering;

public sealed record PreviewRequest(
    Guid? TemplateVersionId,
    TemplateContentInput? Content,
    IReadOnlyDictionary<string, string?> Variables,
    string Mode); // "sample" | "strict"

public sealed record WarningDto(string Code, string Message, int? Line);

public sealed record PreviewResult(
    string Subject, string? Preheader, string Html, string Text, IReadOnlyList<WarningDto> Warnings);

public sealed record ValidateResult(bool Valid, IReadOnlyList<object> Errors, IReadOnlyList<WarningDto> Warnings);

public sealed class RenderHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITemplateRenderer renderer,
    IObjectStorage storage,
    IOptions<StorageOptions> storageOptions)
{
    public async Task<PreviewResult> PreviewAsync(PreviewRequest request, CancellationToken ct)
    {
        var content = await ResolveContentAsync(request, ct);
        var strict = string.Equals(request.Mode, "strict", StringComparison.OrdinalIgnoreCase);
        var variables = BuildVariables(content, request.Variables, strict);
        var assetUrls = await ResolveAssetUrlsAsync(content.Assets, ct);

        var rendered = renderer.Render(new RenderRequest(content, variables, strict, assetUrls));
        return new PreviewResult(
            rendered.Subject, rendered.Preheader, rendered.Html, rendered.Text,
            rendered.Warnings.Select(w => new WarningDto(w.Code, w.Message, w.Line)).ToList());
    }

    /// <summary>Non-throwing validation: compiles/renders and reports errors + warnings.</summary>
    public async Task<ValidateResult> ValidateAsync(PreviewRequest request, CancellationToken ct)
    {
        try
        {
            var content = await ResolveContentAsync(request, ct);
            var variables = BuildVariables(content, request.Variables, strict: false);
            var assetUrls = await ResolveAssetUrlsAsync(content.Assets, ct);
            var rendered = renderer.Render(new RenderRequest(content, variables, Strict: false, assetUrls));
            return new ValidateResult(true, [],
                rendered.Warnings.Select(w => new WarningDto(w.Code, w.Message, w.Line)).ToList());
        }
        catch (MjmlInvalidException ex)
        {
            return new ValidateResult(false,
                ex.Errors.Select(e => (object)new { e.Line, e.Column, e.Message }).ToList(), []);
        }
    }

    private async Task<TemplateContent> ResolveContentAsync(PreviewRequest request, CancellationToken ct)
    {
        if (request.TemplateVersionId is { } versionId)
        {
            var version = await db.EmailTemplateVersions
                .Include(v => v.TemplateAssets)
                .Include(v => v.Template!)
                .FirstOrDefaultAsync(v => v.Id == versionId && v.Template!.UserId == currentUser.UserId, ct)
                ?? throw new NotFoundException();
            return TemplateContentMapping.ToRenderContent(version);
        }

        if (request.Content is { } input)
        {
            return MapInlineContent(input);
        }

        throw new ValidationFailure("source", "Either templateVersionId or content is required.");
    }

    private static TemplateContent MapInlineContent(TemplateContentInput input)
    {
        var editorKind = Enum.TryParse<EditorKind>(input.EditorKind, ignoreCase: true, out var k)
            ? k : EditorKind.Html;
        var variables = input.Variables
            .Select(v => new TemplateVariable(
                v.Name,
                Enum.TryParse<TemplateVariableType>(v.Type, ignoreCase: true, out var t) ? t : TemplateVariableType.Text,
                v.Required, v.Default, v.Sample))
            .ToList();
        var assets = input.Assets
            .Select(a => new TemplateAssetRef(
                a.AssetId,
                TemplateAssetUsageParser.TryParse(a.Usage, out var u) ? u : TemplateAssetUsage.HostedImage,
                a.ContentId))
            .ToList();
        return new TemplateContent(
            input.Subject, input.Preheader, editorKind, input.MjmlSource,
            input.HtmlBody, input.TextBody, variables, assets);
    }

    private static IReadOnlyDictionary<string, string?> BuildVariables(
        TemplateContent content, IReadOnlyDictionary<string, string?> provided, bool strict)
    {
        if (strict) return provided;

        // Sample mode: fill unspecified variables from their sample/default values.
        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var variable in content.Variables)
        {
            merged[variable.Name] = variable.Sample ?? variable.Default;
        }
        foreach (var (key, value) in provided)
        {
            if (!string.IsNullOrEmpty(value)) merged[key] = value;
        }
        return merged;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveAssetUrlsAsync(
        IReadOnlyList<TemplateAssetRef> assets, CancellationToken ct)
    {
        var map = new Dictionary<Guid, string>();
        if (assets.Count == 0) return map;

        var ids = assets.Select(a => a.AssetId).Distinct().ToList();
        var options = storageOptions.Value;
        var owned = await db.Assets
            .Where(a => ids.Contains(a.Id)
                        && a.UserId == currentUser.UserId
                        && a.UploadState == AssetUploadState.Ready)
            .ToListAsync(ct);

        foreach (var asset in owned)
        {
            // Public assets use their stable URL; private ones a short-lived preview URL.
            map[asset.Id] = asset is { Access: AssetAccess.Public, PublicUrl: { } url }
                ? url
                : await storage.CreatePresignedDownloadUrlAsync(
                    options.PrivateBucket, asset.StorageKey,
                    TimeSpan.FromMinutes(options.DownloadUrlExpiryMinutes), ct);
        }
        return map;
    }
}
