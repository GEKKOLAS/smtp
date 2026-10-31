using System.Text.RegularExpressions;
using MimeKit;

namespace MailTemplateHub.Infrastructure.Sending;

/// <summary>One recipient a delivery report says failed permanently.</summary>
internal sealed record BounceNotice(Guid RecipientRef, string? FinalRecipient, string Status, string? Diagnostic);

/// <summary>
/// Reads RFC 3464 delivery reports (multipart/report with message/delivery-status),
/// the format both Gmail and Exchange use for bounces (spec 14 F4.3). A report
/// counts only when it carries our X-MailTemplateHub-Ref from the original
/// headers: anything unmatched is ignored, never guessed.
/// </summary>
internal static partial class DeliveryStatusParser
{
    public const string ReferenceHeader = "X-MailTemplateHub-Ref";

    public static IReadOnlyList<BounceNotice> Parse(byte[] raw)
    {
        MimeMessage message;
        try
        {
            using var stream = new MemoryStream(raw);
            message = MimeMessage.Load(stream);
        }
        catch (FormatException)
        {
            return [];
        }

        var report = message.BodyParts.OfType<MessageDeliveryStatus>().FirstOrDefault();
        if (report is null || FindReference(message) is not { } reference) return [];

        var notices = new List<BounceNotice>();
        // The first group holds per-message fields; each later one describes a recipient.
        foreach (var group in report.StatusGroups.Skip(1))
        {
            var action = group["Action"]?.Trim();
            var status = StatusCodeRegex().Match(group["Status"] ?? "").Value;
            // Hard failures only: 4.x.x is "delayed" or a temporary problem.
            if (!string.Equals(action, "failed", StringComparison.OrdinalIgnoreCase) || !status.StartsWith('5')) continue;

            notices.Add(new BounceNotice(
                reference, Address(group["Final-Recipient"]), status, Diagnostic(group["Diagnostic-Code"])));
        }
        return notices;
    }

    private static Guid? FindReference(MimeMessage message)
    {
        // message/rfc822 and text/rfc822-headers both parse into a MessagePart.
        foreach (var part in message.BodyParts.OfType<MessagePart>())
        {
            if (Guid.TryParse(part.Message?.Headers[ReferenceHeader]?.Trim(), out var id)) return id;
        }
        // Some servers paste the original headers into the human-readable part instead.
        foreach (var text in message.BodyParts.OfType<TextPart>())
        {
            var match = ReferenceRegex().Match(text.Text ?? "");
            if (match.Success && Guid.TryParse(match.Groups[1].Value, out var id)) return id;
        }
        return null;
    }

    /// <summary>"rfc822; ada@example.com" → "ada@example.com".</summary>
    private static string? Address(string? field) =>
        field is null ? null : field[(field.IndexOf(';') + 1)..].Trim();

    private static string? Diagnostic(string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        var text = WhitespaceRegex().Replace(field[(field.IndexOf(';') + 1)..], " ").Trim();
        return text.Length > 300 ? text[..300] : text;
    }

    [GeneratedRegex(@"\d\.\d{1,3}\.\d{1,3}")]
    private static partial Regex StatusCodeRegex();

    [GeneratedRegex(@"X-MailTemplateHub-Ref:\s*([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
