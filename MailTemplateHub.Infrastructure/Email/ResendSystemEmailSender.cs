using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Email;

/// <summary>
/// Sends transactional system mail through Resend's HTTP API. Failures are logged
/// and swallowed: the reset endpoint answers 202 whether or not the address
/// exists, so throwing here would both leak that distinction and 500 the caller.
/// </summary>
internal sealed class ResendSystemEmailSender(
    HttpClient httpClient,
    IOptions<EmailSenderOptions> emailOptions,
    IOptions<OAuthGeneralOptions> oauthOptions,
    ILogger<ResendSystemEmailSender> logger) : ISystemEmailSender
{
    public async Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        var options = emailOptions.Value;
        var resetUrl = $"{oauthOptions.Value.FrontendBaseUrl.TrimEnd('/')}"
                       + $"/reset-password?token={Uri.EscapeDataString(token)}";

        var payload = new
        {
            from = $"{options.FromName} <{options.FromAddress}>",
            to = new[] { email },
            subject = "Reset your Mail Template Hub password",
            html = BuildHtml(resetUrl),
            text = BuildText(resetUrl),
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.ApiUrl)
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Resend rejected the password reset for {Email}: {Status} {Reason}",
                    email, (int)response.StatusCode, ExtractMessage(body));
                return;
            }

            logger.LogInformation("Password reset email sent to {Email}.", email);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Could not reach Resend to send the password reset for {Email}.", email);
        }
    }

    /// <summary>Resend reports the cause in a "message" field; keep it out of the user's response.</summary>
    private static string? ExtractMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildHtml(string resetUrl) =>
        $"""
        <div style="font-family:system-ui,-apple-system,Segoe UI,sans-serif;font-size:15px;color:#18181b">
          <h2 style="margin:0 0 16px;font-size:18px">Reset your password</h2>
          <p style="margin:0 0 16px">
            We received a request to reset your Mail Template Hub password.
            This link expires in 30 minutes and can be used once.
          </p>
          <p style="margin:0 0 24px">
            <a href="{resetUrl}"
               style="display:inline-block;padding:10px 18px;border-radius:8px;background:#6d28d9;color:#fff;text-decoration:none">
              Choose a new password
            </a>
          </p>
          <p style="margin:0;color:#71717a;font-size:13px">
            If you didn't ask for this, you can ignore this email — your password stays unchanged.
          </p>
        </div>
        """;

    private static string BuildText(string resetUrl) =>
        $"""
        Reset your password

        We received a request to reset your Mail Template Hub password.
        This link expires in 30 minutes and can be used once:

        {resetUrl}

        If you didn't ask for this, you can ignore this email — your password stays unchanged.
        """;
}
