using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Infrastructure.Email;
using MimeKit;
using MailboxAddress = MailTemplateHub.Application.Abstractions.Email.MailboxAddress;

namespace MailTemplateHub.UnitTests.Sending;

public class MimeKitEmailMessageBuilderTests
{
    private readonly MimeKitEmailMessageBuilder _builder = new();

    private static OutgoingEmail Email(
        IReadOnlyList<CidAttachment>? inline = null,
        IReadOnlyList<FileAttachment>? attachments = null) => new(
        new MailboxAddress("sender@example.com", "Sender"),
        [new MailboxAddress("to@example.com", "Recipient")],
        "Subject line",
        "<p>Hello</p>",
        "Hello",
        inline ?? [],
        attachments ?? [],
        new Dictionary<string, string> { ["X-MailTemplateHub-Ref"] = "abc" });

    private static MimeMessage Parse(BuiltMimeMessage built)
    {
        built.Rfc822Stream.Position = 0;
        return MimeMessage.Load(built.Rfc822Stream);
    }

    [Fact]
    public void Builds_text_and_html_alternative()
    {
        using var built = _builder.Build(Email());
        var message = Parse(built);

        Assert.Equal("Subject line", message.Subject);
        Assert.Equal("to@example.com", ((MimeKit.MailboxAddress)message.To[0]).Address);
        Assert.Contains("Hello", message.HtmlBody);
        Assert.Equal("Hello", message.TextBody);
        Assert.Equal("abc", message.Headers["X-MailTemplateHub-Ref"]);
    }

    [Fact]
    public void Includes_custom_and_generated_headers()
    {
        using var built = _builder.Build(Email());
        var message = Parse(built);

        Assert.False(string.IsNullOrEmpty(message.MessageId));
        Assert.NotEqual(default, message.Date);
    }

    [Fact]
    public void Embeds_cid_inline_image()
    {
        var inline = new CidAttachment("logo@mth", "logo.png", "image/png", [0x89, 0x50, 0x4E, 0x47]);
        using var built = _builder.Build(Email(inline: [inline]));
        var message = Parse(built);

        var related = message.BodyParts.OfType<MimePart>()
            .FirstOrDefault(p => p.ContentId == "logo@mth");
        Assert.NotNull(related);
        Assert.Equal("image/png", related!.ContentType.MimeType);
    }

    [Fact]
    public void Adds_file_attachment()
    {
        var attachment = new FileAttachment("report.pdf", "application/pdf", "%PDF-1.4"u8.ToArray());
        using var built = _builder.Build(Email(attachments: [attachment]));
        var message = Parse(built);

        var att = message.Attachments.OfType<MimePart>().FirstOrDefault();
        Assert.NotNull(att);
        Assert.Equal("report.pdf", att!.FileName);
    }

    [Fact]
    public void Reports_a_positive_size()
    {
        using var built = _builder.Build(Email());
        Assert.True(built.SizeBytes > 0);
        Assert.True(_builder.EstimateSize(Email()) > 0);
    }
}
