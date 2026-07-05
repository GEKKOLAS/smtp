using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Features.Templates;

// ---- Inputs ----

public sealed record VariableInput(string Name, string Type, bool Required, string? Default, string? Sample);

public sealed record TemplateAssetInput(Guid AssetId, string Usage, string? ContentId);

public sealed record TemplateContentInput(
    string EditorKind,
    string Subject,
    string? Preheader,
    string? MjmlSource,
    JsonElement? GrapesProject,
    string HtmlBody,
    string? TextBody,
    IReadOnlyList<VariableInput> Variables,
    IReadOnlyList<TemplateAssetInput> Assets);

// ---- Outputs ----

public sealed record TemplateSummaryDto(
    Guid Id, string Name, string? Description, bool IsArchived,
    int? CurrentVersionNumber, DateTimeOffset UpdatedAt);

public sealed record TemplateAssetDto(Guid AssetId, string Usage, string? ContentId);

public sealed record TemplateVersionDto(
    Guid Id, int VersionNumber, string Subject, string? Preheader, string EditorKind,
    string? MjmlSource, JsonElement? GrapesProject, string HtmlBody, string? TextBody,
    JsonElement VariablesSchema, IReadOnlyList<TemplateAssetDto> Assets, DateTimeOffset CreatedAt)
{
    public static TemplateVersionDto From(EmailTemplateVersion v) => new(
        v.Id, v.VersionNumber, v.Subject, v.Preheader, v.EditorKind.ToString().ToLowerInvariant(),
        v.MjmlSource,
        v.GrapesProject is null ? null : JsonDocument.Parse(v.GrapesProject.RootElement.GetRawText()).RootElement,
        v.HtmlBody, v.TextBody,
        JsonDocument.Parse(v.VariablesSchema.RootElement.GetRawText()).RootElement,
        v.TemplateAssets.Select(a => new TemplateAssetDto(a.AssetId, a.Usage.ToString().ToLowerInvariant(), a.ContentId)).ToList(),
        v.CreatedAt);
}

public sealed record TemplateVersionSummaryDto(Guid Id, int VersionNumber, string EditorKind, DateTimeOffset CreatedAt);

public sealed record TemplateDto(
    Guid Id, string Name, string? Description, bool IsArchived,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, TemplateVersionDto? CurrentVersion)
{
    public static TemplateDto From(EmailTemplate t) => new(
        t.Id, t.Name, t.Description, t.IsArchived, t.CreatedAt, t.UpdatedAt,
        t.CurrentVersion is null ? null : TemplateVersionDto.From(t.CurrentVersion));
}

public static class TemplateContentMapping
{
    /// <summary>Maps a stored version to render-ready content. Uses the compiled
    /// HTML directly (no recompile) since it was validated at save time.</summary>
    public static TemplateContent ToRenderContent(EmailTemplateVersion version) => new(
        version.Subject,
        version.Preheader,
        EditorKind.Html, // render from the stored compiled HTML
        MjmlSource: null,
        version.HtmlBody,
        version.TextBody,
        ParseVariables(version.VariablesSchema.RootElement),
        version.TemplateAssets
            .Select(a => new TemplateAssetRef(a.AssetId, a.Usage, a.ContentId))
            .ToList());

    public static IReadOnlyList<TemplateVariable> ParseVariables(JsonElement schema)
    {
        var list = new List<TemplateVariable>();
        if (schema.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in schema.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var type = item.TryGetProperty("type", out var t)
                ? Enum.Parse<TemplateVariableType>(t.GetString()!, ignoreCase: true)
                : TemplateVariableType.Text;
            var required = item.TryGetProperty("required", out var r) && r.GetBoolean();
            var def = item.TryGetProperty("default", out var d) ? d.GetString() : null;
            var sample = item.TryGetProperty("sample", out var s) ? s.GetString() : null;
            list.Add(new TemplateVariable(name, type, required, def, sample));
        }
        return list;
    }
}
