namespace MailTemplateHub.Domain.Enums;

public enum AssetKind
{
    Image,
    Gif,
    Document,
    Archive,
    Other,
}

public enum AssetAccess
{
    Private,
    Public,
}

public enum AssetUploadState
{
    /// <summary>Row created; presigned PUT issued; object not yet verified.</summary>
    Pending,

    /// <summary>Object verified and available.</summary>
    Ready,

    /// <summary>Verification failed; object removed.</summary>
    Rejected,
}
