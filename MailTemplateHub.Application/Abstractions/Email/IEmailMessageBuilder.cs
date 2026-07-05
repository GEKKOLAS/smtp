namespace MailTemplateHub.Application.Abstractions.Email;

public sealed record BuiltMimeMessage(Stream Rfc822Stream, long SizeBytes) : IDisposable
{
    public void Dispose() => Rfc822Stream.Dispose();
}

/// <summary>
/// Builds a MIME message from an <see cref="OutgoingEmail"/> (MimeKit lives behind
/// this). Used both to send and to size-check before queueing (spec 07 §5, 14).
/// </summary>
public interface IEmailMessageBuilder
{
    /// <summary>Serializes to an RFC 2822 stream; caller disposes.</summary>
    BuiltMimeMessage Build(OutgoingEmail email);

    /// <summary>Estimated serialized size without building the full stream.</summary>
    long EstimateSize(OutgoingEmail email);
}
