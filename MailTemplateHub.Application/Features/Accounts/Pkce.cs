using System.Security.Cryptography;
using System.Text;

namespace MailTemplateHub.Application.Features.Accounts;

/// <summary>PKCE (RFC 7636) S256 code verifier/challenge, used for both providers.</summary>
public static class Pkce
{
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(64));

    public static string Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
