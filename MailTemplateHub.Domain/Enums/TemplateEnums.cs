namespace MailTemplateHub.Domain.Enums;

public enum EditorKind
{
    /// <summary>Built visually in GrapesJS; MJML is the source of truth.</summary>
    Visual,

    /// <summary>Authored directly in MJML.</summary>
    Mjml,

    /// <summary>Raw HTML (imported or hand-written).</summary>
    Html,
}

public enum TemplateAssetUsage
{
    /// <summary>Embedded in the MIME message and referenced by a cid: URI.</summary>
    InlineCid,

    /// <summary>Referenced by a public HTTPS URL.</summary>
    HostedImage,

    /// <summary>Sent as a normal file attachment.</summary>
    Attachment,
}

public enum TemplateVariableType
{
    Text,
    Url,
    Html,
}
