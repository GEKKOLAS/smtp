namespace MailTemplateHub.Application.Abstractions.Oauth;

/// <summary>The provider rejected the refresh grant permanently (e.g. invalid_grant).</summary>
public sealed class RefreshTokenRevokedException(string? providerCode = null)
    : Exception("The provider refresh token is no longer valid.")
{
    public string? ProviderCode { get; } = providerCode;
}

/// <summary>A transient provider failure during an OAuth HTTP call (5xx / network).</summary>
public sealed class OAuthTransientException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Code exchange failed for a non-retryable reason (bad code, config).</summary>
public sealed class OAuthExchangeException(string message)
    : Exception(message);
