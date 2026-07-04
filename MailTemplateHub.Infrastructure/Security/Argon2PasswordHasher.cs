using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using MailTemplateHub.Application.Abstractions;

namespace MailTemplateHub.Infrastructure.Security;

/// <summary>
/// Argon2id per spec 04-security.md §1: 64 MB memory, 3 iterations, parallelism 2.
/// Encoded as $argon2id$v=19$m=..,t=..,p=..$&lt;salt&gt;$&lt;hash&gt; (unpadded base64)
/// so parameters travel with the hash and NeedsRehash can detect upgrades.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemoryKb = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 2;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt, MemoryKb, Iterations, Parallelism);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${Encode(salt)}${Encode(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        if (!TryParse(encodedHash, out var parsed)) return false;
        var actual = Compute(password, parsed.Salt, parsed.MemoryKb, parsed.Iterations, parsed.Parallelism);
        return CryptographicOperations.FixedTimeEquals(actual, parsed.Hash);
    }

    public bool NeedsRehash(string encodedHash) =>
        !TryParse(encodedHash, out var parsed)
        || parsed.MemoryKb != MemoryKb
        || parsed.Iterations != Iterations
        || parsed.Parallelism != Parallelism;

    private static byte[] Compute(string password, byte[] salt, int memoryKb, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon2.GetBytes(HashSize);
    }

    private static bool TryParse(
        string encoded,
        out (byte[] Salt, byte[] Hash, int MemoryKb, int Iterations, int Parallelism) parsed)
    {
        parsed = default;
        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        // [ argon2id, v=19, m=..,t=..,p=.., salt, hash ]
        if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19") return false;

        int memoryKb = 0, iterations = 0, parallelism = 0;
        foreach (var param in parts[2].Split(','))
        {
            var kv = param.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], out var value)) return false;
            switch (kv[0])
            {
                case "m": memoryKb = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
                default: return false;
            }
        }
        if (memoryKb <= 0 || iterations <= 0 || parallelism <= 0) return false;

        try
        {
            parsed = (Decode(parts[3]), Decode(parts[4]), memoryKb, iterations, parallelism);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] Decode(string value) =>
        Convert.FromBase64String(value.PadRight(value.Length + (4 - value.Length % 4) % 4, '='));
}
