namespace MailTemplateHub.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);

    /// <summary>True when the stored hash uses outdated parameters (rehash on next login).</summary>
    bool NeedsRehash(string encodedHash);
}
