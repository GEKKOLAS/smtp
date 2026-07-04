using System.Security.Cryptography;
using System.Text;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure.Security;

/// <summary>
/// AES-256-GCM envelope encryption (spec 04-security.md §2):
/// a random per-account DEK is wrapped by a versioned KEK; each secret is
/// encrypted with the DEK using the account id as associated data so a
/// ciphertext moved to another row fails authentication.
/// </summary>
public sealed class AesGcmTokenCipher : ITokenCipher
{
    private const int KeySize = 32;   // AES-256
    private const int NonceSize = 12; // GCM standard
    private const int TagSize = 16;

    private readonly IReadOnlyDictionary<int, byte[]> _keks;
    private readonly int _activeKekVersion;

    public AesGcmTokenCipher(IOptions<TokenCryptoOptions> options)
    {
        var value = options.Value;
        _keks = value.Keys.ToDictionary(kv => kv.Key, kv => DecodeKek(kv.Key, kv.Value));
        if (!_keks.ContainsKey(value.ActiveKekVersion))
        {
            throw new InvalidOperationException(
                $"Active KEK version {value.ActiveKekVersion} is not present in TokenCrypto:Keys.");
        }
        _activeKekVersion = value.ActiveKekVersion;
    }

    public (byte[] WrappedDek, int KekVersion) CreateDataKey()
    {
        var dek = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            return (WrapDek(dek, _activeKekVersion), _activeKekVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public EncryptedSecret Encrypt(byte[] wrappedDek, int kekVersion, string plaintext, Guid accountId)
    {
        var dek = UnwrapDek(wrappedDek, kekVersion);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(dek, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, accountId.ToByteArray());

            // Store ciphertext||tag together; nonce alongside.
            return new EncryptedSecret([.. ciphertext, .. tag], nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public string Decrypt(byte[] wrappedDek, int kekVersion, EncryptedSecret secret, Guid accountId)
    {
        var dek = UnwrapDek(wrappedDek, kekVersion);
        try
        {
            var combined = secret.Ciphertext;
            var ciphertext = combined.AsSpan(0, combined.Length - TagSize);
            var tag = combined.AsSpan(combined.Length - TagSize);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(dek, TagSize);
            aes.Decrypt(secret.Nonce, ciphertext, tag, plaintext, accountId.ToByteArray());

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    // DEK wrapping is itself AES-GCM under the KEK, nonce prepended to the payload.
    private byte[] WrapDek(byte[] dek, int kekVersion)
    {
        var kek = _keks[kekVersion];
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[dek.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(kek, TagSize);
        aes.Encrypt(nonce, dek, ciphertext, tag);
        return [.. nonce, .. ciphertext, .. tag];
    }

    private byte[] UnwrapDek(byte[] wrapped, int kekVersion)
    {
        if (!_keks.TryGetValue(kekVersion, out var kek))
        {
            throw new InvalidOperationException($"KEK version {kekVersion} is not configured.");
        }

        var nonce = wrapped.AsSpan(0, NonceSize);
        var tag = wrapped.AsSpan(wrapped.Length - TagSize);
        var ciphertext = wrapped.AsSpan(NonceSize, wrapped.Length - NonceSize - TagSize);
        var dek = new byte[ciphertext.Length];

        using var aes = new AesGcm(kek, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, dek);
        return dek;
    }

    private static byte[] DecodeKek(int version, string base64)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"TokenCrypto KEK version {version} is not valid base64.", ex);
        }
        if (key.Length != KeySize)
        {
            throw new InvalidOperationException(
                $"TokenCrypto KEK version {version} must be {KeySize} bytes (got {key.Length}).");
        }
        return key;
    }
}
