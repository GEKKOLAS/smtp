using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// Join between a template version and an asset it uses, recording how the asset
/// is embedded (inline CID / hosted image / attachment) (spec 05-database.md).
/// </summary>
public sealed class TemplateAsset
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TemplateVersionId { get; set; } // set by EF via the version graph
    public required Guid AssetId { get; init; }
    public required TemplateAssetUsage Usage { get; init; }

    /// <summary>The cid: value; required when Usage is InlineCid.</summary>
    public string? ContentId { get; init; }

    public DateTimeOffset CreatedAt { get; set; }

    public Asset? Asset { get; init; }
}
