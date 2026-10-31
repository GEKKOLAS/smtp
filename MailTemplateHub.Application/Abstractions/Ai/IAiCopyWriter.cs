using MailTemplateHub.Application.Common;

namespace MailTemplateHub.Application.Abstractions.Ai;

/// <summary>What a piece of copy is, which sets its length and shape (spec 14 F2).</summary>
public static class CopyKinds
{
    public const string Subject = "subject";
    public const string Preheader = "preheader";
    public const string Body = "body";
    public const string Cta = "cta";

    public static readonly IReadOnlyList<string> All = [Subject, Preheader, Body, Cta];

    public static int MaxLength(string kind) => kind == Body ? 2000 : 200;
}

public static class CopyTones
{
    public const string Promotional = "promotional";
    public const string Professional = "professional";
    public const string Technical = "technical";
    public const string FollowUp = "followUp";

    public static readonly IReadOnlyList<string> All = [Promotional, Professional, Technical, FollowUp];
}

public sealed record CopyRequest(string Kind, string Text, string Tone, int Count, string? BrandVoice, string? Context);

public sealed record CopyVariation(string Text, string? Note);

/// <summary>
/// Rewrites a short piece of email copy in a given tone. Only a real model makes
/// sense here, so without a key the feature is off (<see cref="IsConfigured"/>).
/// </summary>
public interface IAiCopyWriter
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<CopyVariation>> RewriteAsync(CopyRequest request, CancellationToken ct);
}

/// <summary>An optional AI feature is not configured on this server (maps to 503).</summary>
public sealed class AiUnavailableException(string message) : AppException("ai.unavailable", message);
