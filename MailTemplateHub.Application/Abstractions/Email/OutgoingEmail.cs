namespace MailTemplateHub.Application.Abstractions.Email;

public sealed record MailboxAddress(string Email, string? Name);

/// <summary>An image embedded in the MIME message, referenced by cid: in the HTML.</summary>
public sealed record CidAttachment(string ContentId, string FileName, string MimeType, byte[] Content);

public sealed record FileAttachment(string FileName, string MimeType, byte[] Content);

/// <summary>Provider-agnostic outgoing message assembled by the send pipeline (spec 03 §3).</summary>
public sealed record OutgoingEmail(
    MailboxAddress From,
    IReadOnlyList<MailboxAddress> To,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<CidAttachment> InlineAssets,
    IReadOnlyList<FileAttachment> Attachments,
    IReadOnlyDictionary<string, string> Headers,
    // Provider-side conversation to append to. Gmail needs it in the send
    // payload; Outlook threads purely on the In-Reply-To/References headers.
    string? ProviderThreadId = null);
