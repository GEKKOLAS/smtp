namespace MailTemplateHub.Domain.Entities;

/// <summary>
/// One link in one recipient's copy of a tracked send (spec 14 F4.1). The
/// redirect endpoint resolves the token to <see cref="TargetUrl"/>; the
/// destination never comes from the request, so it is not an open redirect.
/// </summary>
public sealed class TrackedLink
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid SendJobId { get; init; }
    public required Guid RecipientId { get; init; }

    /// <summary>16 random bytes, base64url (22 characters).</summary>
    public required string Token { get; init; }

    /// <summary>Where the click goes: the link as written, plus UTM tags when the send has a campaign.</summary>
    public required string TargetUrl { get; init; }

    /// <summary>Order of first appearance in the email, from 0.</summary>
    public int Position { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public EmailSendRecipient? Recipient { get; init; }
    public List<LinkClick> Clicks { get; init; } = [];
}

/// <summary>
/// A followed tracked link. No IP address or user agent is stored: only when it
/// happened and whether it looked like a person or a link scanner.
/// </summary>
public sealed class LinkClick
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid TrackedLinkId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary><see cref="ClickClassifications.Human"/> or <see cref="ClickClassifications.Scanner"/>.</summary>
    public required string Classification { get; init; }
}

public static class ClickClassifications
{
    public const string Human = "human";
    public const string Scanner = "scanner";
}
