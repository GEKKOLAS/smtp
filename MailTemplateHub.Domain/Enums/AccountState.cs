namespace MailTemplateHub.Domain.Enums;

public enum AccountState
{
    /// <summary>Tokens valid; account can send.</summary>
    Active,

    /// <summary>Refresh failed or scopes insufficient; user must reconnect.</summary>
    NeedsReconnect,

    /// <summary>User disconnected; tokens wiped.</summary>
    Revoked,
}

public static class AccountStateReasons
{
    public const string InvalidGrant = "invalid_grant";
    public const string InsufficientScope = "insufficient_scope";
    public const string UserDisconnect = "user_disconnect";
}
