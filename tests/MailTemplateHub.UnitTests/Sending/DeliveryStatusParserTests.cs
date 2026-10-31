using System.Text;
using MailTemplateHub.Infrastructure.Sending;

namespace MailTemplateHub.UnitTests.Sending;

public class DeliveryStatusParserTests
{
    private static readonly Guid Ref = Guid.Parse("019a3f6e-1d2c-7c3b-9a8e-0f1e2d3c4b5a");

    /// <summary>A multipart/report shaped like Gmail's (original attached as message/rfc822).</summary>
    private static byte[] Report(string action, string status, string originalPart, string? explanation = null) =>
        Encoding.UTF8.GetBytes($"""
            From: Mail Delivery Subsystem <mailer-daemon@googlemail.com>
            To: sender@gmail.com
            Subject: Delivery Status Notification (Failure)
            MIME-Version: 1.0
            Content-Type: multipart/report; boundary="b1"; report-type=delivery-status

            --b1
            Content-Type: text/plain; charset="UTF-8"

            {explanation ?? "Your message wasn't delivered to nobody@example.com."}

            --b1
            Content-Type: message/delivery-status

            Reporting-MTA: dns; googlemail.com

            Final-Recipient: rfc822; nobody@example.com
            Action: {action}
            Status: {status}
            Diagnostic-Code: smtp; 550-5.1.1 The email account that you tried to reach does not exist.
             Please try double-checking the recipient's email address.

            --b1
            {originalPart}
            --b1--
            """.ReplaceLineEndings("\r\n"));

    private static string AttachedMessage(Guid? reference) => $"""
        Content-Type: message/rfc822

        From: sender@gmail.com
        To: nobody@example.com
        Subject: Spring menu
        {(reference is null ? "" : $"X-MailTemplateHub-Ref: {reference}")}
        Message-ID: <abc@mail.gmail.com>

        Hello
        """;

    [Fact]
    public void Gmail_style_hard_bounce_is_matched_by_the_reference_header()
    {
        var notices = DeliveryStatusParser.Parse(Report("failed", "5.1.1", AttachedMessage(Ref)));

        var notice = Assert.Single(notices);
        Assert.Equal(Ref, notice.RecipientRef);
        Assert.Equal("nobody@example.com", notice.FinalRecipient);
        Assert.Equal("5.1.1", notice.Status);
        Assert.StartsWith("550-5.1.1 The email account that you tried to reach does not exist. Please try", notice.Diagnostic);
    }

    [Fact]
    public void Exchange_style_report_with_only_the_original_headers_works_too()
    {
        var headersOnly = $"""
            Content-Type: text/rfc822-headers

            From: sender@outlook.com
            To: nobody@example.com
            X-MailTemplateHub-Ref: {Ref}
            Subject: Spring menu

            """;

        var notice = Assert.Single(DeliveryStatusParser.Parse(Report("failed", "5.4.1", headersOnly)));
        Assert.Equal(Ref, notice.RecipientRef);
        Assert.Equal("5.4.1", notice.Status);
    }

    [Fact]
    public void Reference_pasted_into_the_text_part_is_a_fallback()
    {
        var plainOriginal = """
            Content-Type: text/plain

            (original message not included)
            """;

        var notices = DeliveryStatusParser.Parse(Report("failed", "5.1.1", plainOriginal,
            explanation: $"Original headers:\nX-MailTemplateHub-Ref: {Ref}"));

        Assert.Equal(Ref, Assert.Single(notices).RecipientRef);
    }

    [Theory]
    [InlineData("delayed", "4.4.7")]
    [InlineData("failed", "4.2.2")]
    [InlineData("delivered", "2.0.0")]
    public void Delays_and_temporary_failures_are_not_bounces(string action, string status) =>
        Assert.Empty(DeliveryStatusParser.Parse(Report(action, status, AttachedMessage(Ref))));

    [Fact]
    public void Reports_without_our_reference_are_ignored() =>
        Assert.Empty(DeliveryStatusParser.Parse(Report("failed", "5.1.1", AttachedMessage(null))));

    [Fact]
    public void Ordinary_mail_is_not_a_report() =>
        Assert.Empty(DeliveryStatusParser.Parse(Encoding.UTF8.GetBytes(
            "From: a@b.com\r\nTo: c@d.com\r\nSubject: Hi\r\nContent-Type: text/plain\r\n\r\nHello\r\n")));
}
