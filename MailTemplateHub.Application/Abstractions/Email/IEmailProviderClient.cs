using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Abstractions.Email;

public sealed record ProviderSendResult(string? ProviderMessageId, string? ThreadId, string RawStatus);

/// <summary>Normalized provider error class that drives the retry state machine (spec 03 §3, 07 §3).</summary>
public enum ProviderErrorKind
{
    Transient,          // 429/5xx/network → retry with backoff
    AuthExpired,        // access token rejected → refresh then retry
    AuthRevoked,        // invalid_grant → NeedsReconnect, fail permanent
    InsufficientScope,  // missing send scope → NeedsReconnect
    MessageTooLarge,    // permanent, actionable
    RecipientRejected,  // permanent for that recipient
    QuotaExceeded,      // daily cap → park
    PermanentOther,
}

public sealed class ProviderSendException(
    ProviderErrorKind kind, string safeMessage, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(safeMessage, inner)
{
    public ProviderErrorKind Kind { get; } = kind;
    public string SafeMessage { get; } = safeMessage;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>
/// Sends a fully built MIME message via a provider. No provider SDK type leaks
/// past this port (spec 03 §3); implementations translate errors into
/// <see cref="ProviderSendException"/> with a typed <see cref="ProviderErrorKind"/>.
/// </summary>
public interface IEmailProviderClient
{
    EmailProvider Provider { get; }

    /// <summary>Minimum spacing between sends on one account (throttle, spec 07 §3).</summary>
    TimeSpan MinSendInterval { get; }

    Task<ProviderSendResult> SendAsync(
        ConnectedAccountContext account, OutgoingEmail email, CancellationToken ct);
}

public interface IEmailProviderClientFactory
{
    IEmailProviderClient For(EmailProvider provider);
}
