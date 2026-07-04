using System.Text.Json;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// Append-only log of provider interactions (spec 05-database.md). Sanitized —
/// never stores tokens or full response bodies.
/// </summary>
public sealed class EmailProviderEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid? ConnectedEmailAccountId { get; init; }
    public required EmailProvider Provider { get; init; }
    public required string EventType { get; init; }
    public int? HttpStatus { get; init; }
    public string? ProviderErrorCode { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public JsonDocument? Detail { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public static class ProviderEventTypes
{
    public const string TokenRefresh = "token_refresh";
    public const string TokenRefreshFailed = "token_refresh_failed";
    public const string ConnectionTest = "connection_test";
}
