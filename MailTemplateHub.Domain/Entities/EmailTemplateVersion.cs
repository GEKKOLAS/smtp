using System.Text.Json;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// Immutable snapshot of a template's content (spec 05-database.md). Never updated
/// after insert — edits create a new version.
/// </summary>
public sealed class EmailTemplateVersion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid TemplateId { get; init; }
    public required int VersionNumber { get; init; }

    public required string Subject { get; init; }
    public string? Preheader { get; init; }
    public string? MjmlSource { get; init; }
    public JsonDocument? GrapesProject { get; init; }
    public required string HtmlBody { get; init; }
    public string? TextBody { get; init; }

    /// <summary>Array of { name, type, required, default, sample }.</summary>
    public JsonDocument VariablesSchema { get; init; } = JsonDocument.Parse("[]");

    public required EditorKind EditorKind { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public DateTimeOffset CreatedAt { get; set; }

    public EmailTemplate? Template { get; init; }
    public List<TemplateAsset> TemplateAssets { get; init; } = [];
}
