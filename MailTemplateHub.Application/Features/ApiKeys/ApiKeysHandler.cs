using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.ApiKeys;

public sealed record CreateApiKeyCommand(string Name, int? ExpiresInDays);

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 3650).When(x => x.ExpiresInDays.HasValue);
    }
}

public sealed record ApiKeyDto(
    Guid Id, string Name, string Prefix, DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt, DateTimeOffset? ExpiresAt);

/// <summary>Returned once at creation; carries the full secret which is never stored.</summary>
public sealed record CreatedApiKeyDto(ApiKeyDto Key, string Secret);

public sealed class ApiKeysHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IValidator<CreateApiKeyCommand> validator,
    IClock clock)
{
    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken ct)
    {
        var keys = await db.ApiKeys
            .Where(k => k.UserId == currentUser.UserId && k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
        return keys.Select(ToDto).ToList();
    }

    public async Task<CreatedApiKeyDto> CreateAsync(CreateApiKeyCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var secret = ApiKeyGenerator.NewSecret();
        var key = new ApiKey
        {
            UserId = currentUser.UserId,
            Name = command.Name.Trim(),
            Prefix = ApiKeyGenerator.Prefix(secret),
            KeyHash = ApiKeyGenerator.Hash(secret),
            ExpiresAt = command.ExpiresInDays is { } days ? clock.UtcNow.AddDays(days) : null,
        };
        db.ApiKeys.Add(key);
        audit.Add(AuditActions.ApiKeyCreated, currentUser.UserId, "api_key", key.Id, new { key.Name });
        await db.SaveChangesAsync(ct);

        return new CreatedApiKeyDto(ToDto(key), secret);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var key = await db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == currentUser.UserId && k.RevokedAt == null, ct)
            ?? throw new NotFoundException();

        key.RevokedAt = clock.UtcNow;
        audit.Add(AuditActions.ApiKeyRevoked, currentUser.UserId, "api_key", id);
        await db.SaveChangesAsync(ct);
    }

    private static ApiKeyDto ToDto(ApiKey k) =>
        new(k.Id, k.Name, k.Prefix, k.CreatedAt, k.LastUsedAt, k.ExpiresAt);
}

/// <summary>Generates and hashes API key secrets (prefix mth_, 256-bit random).</summary>
public static class ApiKeyGenerator
{
    private const string Scheme = "mth_";

    public static string NewSecret() =>
        Scheme + Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Prefix(string secret) => secret[..Math.Min(secret.Length, 12)];

    public static byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public static bool LooksLikeKey(string value) => value.StartsWith(Scheme, StringComparison.Ordinal);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
