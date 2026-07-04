namespace MailTemplateHub.Application.Abstractions;

/// <summary>Ciphertext bundle for one secret (spec 04-security.md §2).</summary>
public sealed record EncryptedSecret(byte[] Ciphertext, byte[] Nonce);

/// <summary>
/// Envelope encryption for provider tokens: a per-account DEK (wrapped by a
/// versioned KEK) encrypts each secret with AES-256-GCM, bound to the account id
/// as associated data so a ciphertext copied to another row fails to decrypt.
/// </summary>
public interface ITokenCipher
{
    /// <summary>Generates a fresh wrapped DEK for one account and reports the active KEK version.</summary>
    (byte[] WrappedDek, int KekVersion) CreateDataKey();

    EncryptedSecret Encrypt(byte[] wrappedDek, int kekVersion, string plaintext, Guid accountId);

    string Decrypt(byte[] wrappedDek, int kekVersion, EncryptedSecret secret, Guid accountId);
}
