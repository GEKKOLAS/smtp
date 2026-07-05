using MailTemplateHub.Application.Abstractions.Email;
using MimeKit;
using MimeKit.Utils;

namespace MailTemplateHub.Infrastructure.Email;

/// <summary>
/// Builds RFC 2822 messages with MimeKit (spec 07 §5, 14): a text+html
/// alternative, inline CID images wrapped in multipart/related, and normal file
/// attachments in multipart/mixed. One MIME path serves both providers.
/// </summary>
internal sealed class MimeKitEmailMessageBuilder : IEmailMessageBuilder
{
    public BuiltMimeMessage Build(OutgoingEmail email)
    {
        var message = Compose(email);
        var stream = new MemoryStream();
        message.WriteTo(stream);
        stream.Position = 0;
        return new BuiltMimeMessage(stream, stream.Length);
    }

    public long EstimateSize(OutgoingEmail email)
    {
        using var counting = new CountingStream();
        Compose(email).WriteTo(counting);
        return counting.Written;
    }

    private static MimeMessage Compose(OutgoingEmail email)
    {
        var message = new MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(email.From.Name, email.From.Email));
        foreach (var to in email.To)
        {
            message.To.Add(new MimeKit.MailboxAddress(to.Name, to.Email));
        }
        message.Subject = email.Subject;
        message.Date = DateTimeOffset.UtcNow;
        message.MessageId = MimeUtils.GenerateMessageId();

        foreach (var (key, value) in email.Headers)
        {
            message.Headers.Add(key, value);
        }

        var builder = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody,
        };

        foreach (var inline in email.InlineAssets)
        {
            var resource = builder.LinkedResources.Add(
                inline.FileName, inline.Content, ContentType.Parse(inline.MimeType));
            resource.ContentId = inline.ContentId;
            resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
            {
                FileName = inline.FileName,
            };
        }

        foreach (var attachment in email.Attachments)
        {
            builder.Attachments.Add(
                attachment.FileName, attachment.Content, ContentType.Parse(attachment.MimeType));
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    /// <summary>Discards bytes while counting them, for size estimation.</summary>
    private sealed class CountingStream : Stream
    {
        public long Written { get; private set; }
        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length => Written;
        public override long Position { get => Written; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Written += count;
        public override void Write(ReadOnlySpan<byte> buffer) => Written += buffer.Length;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
