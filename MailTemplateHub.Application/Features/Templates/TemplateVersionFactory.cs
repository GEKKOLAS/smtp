using System.Text.Json;
using System.Text.RegularExpressions;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Templates;

/// <summary>
/// Validates template content and builds an immutable version: compiles MJML
/// (failing fast with positions), checks the variable schema, and validates
/// asset ownership + usage (spec 06 §5, 08). Shared by create and save-version.
/// </summary>
public sealed partial class TemplateVersionFactory(
    IAppDbContext db,
    ICurrentUser currentUser,
    IMjmlCompiler mjmlCompiler,
    IClock clock)
{
    public async Task<EmailTemplateVersion> BuildAsync(
        Guid templateId, int versionNumber, TemplateContentInput input, CancellationToken ct)
    {
        if (!Enum.TryParse<EditorKind>(input.EditorKind, ignoreCase: true, out var editorKind))
        {
            throw new ValidationFailure("content.editorKind", "Unknown editor kind.");
        }
        if (string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Length > 500)
        {
            throw new ValidationFailure("content.subject", "Subject is required and must be 500 characters or fewer.");
        }

        ValidateVariables(input.Variables);

        // Compile MJML when it is the source of truth; store the compiled HTML.
        string htmlBody;
        string? mjmlSource = null;
        if (editorKind is EditorKind.Visual or EditorKind.Mjml)
        {
            if (string.IsNullOrWhiteSpace(input.MjmlSource))
            {
                throw new ValidationFailure("content.mjmlSource", "MJML source is required for this editor kind.");
            }
            var compiled = mjmlCompiler.Compile(input.MjmlSource);
            if (!compiled.Success) throw new MjmlInvalidException(compiled.Errors);
            htmlBody = compiled.Html;
            mjmlSource = input.MjmlSource;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.HtmlBody))
            {
                throw new ValidationFailure("content.htmlBody", "HTML body is required for this editor kind.");
            }
            htmlBody = input.HtmlBody;
        }

        var templateAssets = await BuildAssetsAsync(input.Assets, ct);

        return new EmailTemplateVersion
        {
            TemplateId = templateId,
            VersionNumber = versionNumber,
            Subject = input.Subject,
            Preheader = input.Preheader,
            MjmlSource = mjmlSource,
            GrapesProject = input.GrapesProject is { } g ? JsonSerializer.SerializeToDocument(g) : null,
            HtmlBody = htmlBody,
            TextBody = input.TextBody,
            VariablesSchema = SerializeVariables(input.Variables),
            EditorKind = editorKind,
            CreatedByUserId = currentUser.UserId,
            TemplateAssets = templateAssets,
        };
    }

    private void ValidateVariables(IReadOnlyList<VariableInput> variables)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            if (!VariableNameRegex().IsMatch(variable.Name))
            {
                throw new ValidationFailure("content.variables", $"Invalid variable name '{variable.Name}'.");
            }
            if (!seen.Add(variable.Name))
            {
                throw new ValidationFailure("content.variables", $"Duplicate variable '{variable.Name}'.");
            }
            if (!Enum.TryParse<TemplateVariableType>(variable.Type, ignoreCase: true, out _))
            {
                throw new ValidationFailure("content.variables", $"Unknown variable type '{variable.Type}'.");
            }
        }
    }

    private async Task<List<TemplateAsset>> BuildAssetsAsync(
        IReadOnlyList<TemplateAssetInput> assets, CancellationToken ct)
    {
        if (assets.Count == 0) return [];

        var ids = assets.Select(a => a.AssetId).Distinct().ToList();
        var owned = await db.Assets
            .Where(a => ids.Contains(a.Id)
                        && a.UserId == currentUser.UserId
                        && a.UploadState == AssetUploadState.Ready)
            .Select(a => a.Id)
            .ToListAsync(ct);
        var ownedSet = owned.ToHashSet();

        var result = new List<TemplateAsset>();
        foreach (var asset in assets)
        {
            if (!ownedSet.Contains(asset.AssetId))
            {
                throw new ValidationFailure("content.assets", "An asset was not found.");
            }
            if (!TemplateAssetUsageParser.TryParse(asset.Usage, out var usage))
            {
                throw new ValidationFailure("content.assets", $"Unknown asset usage '{asset.Usage}'.");
            }
            if (usage == TemplateAssetUsage.InlineCid && string.IsNullOrWhiteSpace(asset.ContentId))
            {
                throw new ValidationFailure("content.assets", "Inline assets require a content id.");
            }
            result.Add(new TemplateAsset
            {
                TemplateVersionId = default, // set by EF via the version graph
                AssetId = asset.AssetId,
                Usage = usage,
                ContentId = usage == TemplateAssetUsage.InlineCid ? asset.ContentId : null,
                CreatedAt = clock.UtcNow,
            });
        }
        return result;
    }

    private static JsonDocument SerializeVariables(IReadOnlyList<VariableInput> variables)
    {
        var normalized = variables.Select(v => new
        {
            name = v.Name,
            type = v.Type.ToLowerInvariant(),
            required = v.Required,
            @default = v.Default,
            sample = v.Sample,
        });
        return JsonSerializer.SerializeToDocument(normalized);
    }

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9_]{0,63}$")]
    private static partial Regex VariableNameRegex();
}

/// <summary>Content validation failure surfaced as 422 with a field code.</summary>
public sealed class ValidationFailure(string field, string message) : AppException("validation_failed", message)
{
    public string Field { get; } = field;
}
