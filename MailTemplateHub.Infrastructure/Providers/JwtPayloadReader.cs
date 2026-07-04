using System.Text.Json;

namespace MailTemplateHub.Infrastructure.Providers;

/// <summary>
/// Reads claims from an id_token payload. Signature validation is intentionally
/// skipped: the token is received directly from the provider's token endpoint over
/// a server-to-server TLS call (never via the browser), so its authenticity is
/// established by the transport (OpenID Connect §3.1.3.7 note). Used only for
/// non-security-critical identity claims (tid/oid).
/// </summary>
internal static class JwtPayloadReader
{
    public static string? GetClaim(string? idToken, string claim)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            var json = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(claim, out var value)
                ? value.GetString()
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
