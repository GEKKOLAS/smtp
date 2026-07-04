using System.Security.Cryptography;
using System.Text;

namespace MailTemplateHub.Application.Features.Auth;

public sealed record UserDto(Guid Id, string Email, string DisplayName, Guid? DefaultAccountId, DateTimeOffset CreatedAt);

public sealed record SessionDto(Guid Id, string? Ip, string? UserAgent, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool Current);

/// <summary>Result of register/login: session token is set as a cookie by the API layer only.</summary>
public sealed record AuthResult(UserDto User, string? SessionToken, string? CsrfToken);

public static class AuthTokens
{
    /// <summary>Random 256-bit token; only its SHA-256 goes to the database.</summary>
    public static (string Raw, byte[] Hash) Create()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (raw, HashToken(raw));
    }

    public static byte[] HashToken(string raw) => SHA256.HashData(Encoding.UTF8.GetBytes(raw));

    public static string CreateCsrfToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
