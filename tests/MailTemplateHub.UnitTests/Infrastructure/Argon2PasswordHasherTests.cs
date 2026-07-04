using MailTemplateHub.Infrastructure.Security;

namespace MailTemplateHub.UnitTests.Infrastructure;

public class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_roundtrips()
    {
        var encoded = _hasher.Hash("correct horse battery staple");

        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=2$", encoded);
        Assert.True(_hasher.Verify("correct horse battery staple", encoded));
    }

    [Fact]
    public void Wrong_password_fails_verification()
    {
        var encoded = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("incorrect horse", encoded));
    }

    [Fact]
    public void Same_password_produces_distinct_hashes()
    {
        Assert.NotEqual(_hasher.Hash("password aaaa bbbb"), _hasher.Hash("password aaaa bbbb"));
    }

    [Fact]
    public void Garbage_input_neither_verifies_nor_throws()
    {
        Assert.False(_hasher.Verify("anything", "not-an-argon2-hash"));
        Assert.True(_hasher.NeedsRehash("not-an-argon2-hash"));
    }

    [Fact]
    public void Outdated_parameters_require_rehash()
    {
        var current = _hasher.Hash("password aaaa bbbb");
        var outdated = current.Replace("m=65536", "m=32768");

        Assert.False(_hasher.NeedsRehash(current));
        Assert.True(_hasher.NeedsRehash(outdated));
    }
}
