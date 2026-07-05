using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Abstractions.Rendering;

public sealed record TemplateVariable(
    string Name, TemplateVariableType Type, bool Required, string? Default, string? Sample);

public sealed record TemplateAssetRef(Guid AssetId, TemplateAssetUsage Usage, string? ContentId);

/// <summary>
/// Render-ready content, decoupled from the EF entity so preview can render both
/// saved versions and unsaved editor content (spec 08-rendering.md).
/// </summary>
public sealed record TemplateContent(
    string Subject,
    string? Preheader,
    EditorKind EditorKind,
    string? MjmlSource,
    string HtmlBody,
    string? TextBody,
    IReadOnlyList<TemplateVariable> Variables,
    IReadOnlyList<TemplateAssetRef> Assets);
