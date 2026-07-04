using System.ComponentModel.DataAnnotations;

namespace MailTemplateHub.Application.Common;

public sealed class TokenCryptoOptions
{
    public const string SectionName = "TokenCrypto";

    /// <summary>Version -> base64 32-byte key-encryption key. Rotation adds a new version.</summary>
    [Required]
    public Dictionary<int, string> Keys { get; init; } = [];

    /// <summary>KEK version new DEKs are wrapped with. Must exist in <see cref="Keys"/>.</summary>
    [Range(1, int.MaxValue)]
    public int ActiveKekVersion { get; init; } = 1;
}
