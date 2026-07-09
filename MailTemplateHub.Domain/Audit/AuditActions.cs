namespace MailTemplateHub.Domain.Audit;

/// <summary>Audit action codes (spec 04-security.md §7). Stored as text.</summary>
public static class AuditActions
{
    public const string Register = "auth.register";
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string PasswordReset = "auth.password_reset";
    public const string PasswordChanged = "auth.password_changed";
    public const string SessionRevoked = "auth.session_revoked";

    public const string AccountConnected = "account.connected";
    public const string AccountDisconnected = "account.disconnected";
    public const string AccountDefaultChanged = "account.default_changed";
    public const string OAuthStateRejected = "oauth.state_rejected";
    public const string TokenRefreshFailed = "token.refresh_failed";

    public const string AssetUploaded = "asset.uploaded";
    public const string AssetDeleted = "asset.deleted";
    public const string AssetRejected = "asset.rejected";

    public const string TemplateCreated = "template.created";
    public const string TemplateUpdated = "template.updated";
    public const string TemplateVersionSaved = "template.version_saved";
    public const string TemplateDuplicated = "template.duplicated";
    public const string TemplateArchived = "template.archived";
    public const string TemplateDeleted = "template.deleted";
    public const string TemplateRestored = "template.restored";

    public const string SendCreated = "send.created";
    public const string SendScheduled = "send.scheduled";
    public const string SendCancelled = "send.cancelled";
    public const string SendRetried = "send.retried";
    public const string SendCompleted = "send.completed";
    public const string SendFailed = "send.failed";

    public const string ApiKeyCreated = "api_key.created";
    public const string ApiKeyRevoked = "api_key.revoked";
}
