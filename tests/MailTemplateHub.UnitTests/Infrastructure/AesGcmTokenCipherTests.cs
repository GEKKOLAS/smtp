using System.Security.Cryptography;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.UnitTests.Infrastructure;

public class AesGcmTokenCipherTests
{
    private static string Kek() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmTokenCipher Create(
        Dictionary<int, string>? keys = null, int activeVersion = 1)
    {
        keys ??= new Dictionary<int, string> { [1] = Kek() };
        return new AesGcmTokenCipher(Options.Create(new TokenCryptoOptions
        {
            Keys = keys,
            ActiveKekVersion = activeVersion,
        }));
    }

    [Fact]
    public void Roundtrips_a_token()
    {
        var cipher = Create();
        var accountId = Guid.CreateVersion7();
        var (wrappedDek, kekVersion) = cipher.CreateDataKey();

        var encrypted = cipher.Encrypt(wrappedDek, kekVersion, "ya29.secret-access-token", accountId);
        var decrypted = cipher.Decrypt(wrappedDek, kekVersion, encrypted, accountId);

        Assert.Equal("ya29.secret-access-token", decrypted);
    }

    [Fact]
    public void Ciphertext_does_not_contain_plaintext()
    {
        var cipher = Create();
        var accountId = Guid.CreateVersion7();
        var (wrappedDek, kekVersion) = cipher.CreateDataKey();

        var encrypted = cipher.Encrypt(wrappedDek, kekVersion, "super-secret-value", accountId);

        Assert.DoesNotContain("super-secret-value",
            System.Text.Encoding.UTF8.GetString(encrypted.Ciphertext));
    }

    [Fact]
    public void Decrypt_with_wrong_account_id_fails_authentication()
    {
        var cipher = Create();
        var accountId = Guid.CreateVersion7();
        var (wrappedDek, kekVersion) = cipher.CreateDataKey();
        var encrypted = cipher.Encrypt(wrappedDek, kekVersion, "token", accountId);

        // AAD mismatch: a ciphertext copied to another account row must not decrypt.
        Assert.Throws<AuthenticationTagMismatchException>(() =>
            cipher.Decrypt(wrappedDek, kekVersion, encrypted, Guid.CreateVersion7()));
    }

    [Fact]
    public void Tampered_ciphertext_fails_authentication()
    {
        var cipher = Create();
        var accountId = Guid.CreateVersion7();
        var (wrappedDek, kekVersion) = cipher.CreateDataKey();
        var encrypted = cipher.Encrypt(wrappedDek, kekVersion, "token", accountId);
        encrypted.Ciphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            cipher.Decrypt(wrappedDek, kekVersion, encrypted, accountId));
    }

    [Fact]
    public void Data_keys_are_unique_per_call()
    {
        var cipher = Create();
        Assert.NotEqual(cipher.CreateDataKey().WrappedDek, cipher.CreateDataKey().WrappedDek);
    }

    [Fact]
    public void Supports_decrypting_under_an_older_kek_after_rotation()
    {
        var v1 = Kek();
        var accountId = Guid.CreateVersion7();

        // Encrypt while v1 is active.
        var before = Create(new Dictionary<int, string> { [1] = v1 }, activeVersion: 1);
        var (wrappedDek, kekVersion) = before.CreateDataKey();
        var encrypted = before.Encrypt(wrappedDek, kekVersion, "token", accountId);

        // Rotate: v2 becomes active but v1 is retained for existing rows.
        var after = Create(new Dictionary<int, string> { [1] = v1, [2] = Kek() }, activeVersion: 2);
        Assert.Equal("token", after.Decrypt(wrappedDek, kekVersion, encrypted, accountId));
    }

    [Fact]
    public void Missing_active_kek_throws_on_construction()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Create(new Dictionary<int, string> { [1] = Kek() }, activeVersion: 99));
    }
}
