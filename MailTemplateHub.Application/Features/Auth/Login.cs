using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IRequestContext requestContext,
    IOptions<AuthOptions> authOptions,
    IValidator<LoginCommand> validator,
    IClock clock)
{
    // Argon2id hash of a random throwaway password. Verified against when the
    // account does not exist so unknown-email and wrong-password paths take
    // comparable time (anti-enumeration, spec 04 §1).
    private const string DummyHash =
        "$argon2id$v=19$m=65536,t=3,p=2$AAAAAAAAAAAAAAAAAAAAAA$m0kNXmn3aSAJrLd7RS9GLpSBCPGxDvPYRw2FVFyRj/g";

    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == command.Email, ct);

        if (user is null)
        {
            passwordHasher.Verify(command.Password, DummyHash);
            throw new UnauthorizedAppException("auth.invalid_credentials", "Invalid email or password.");
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            audit.Add(AuditActions.LoginFailed, user.Id, "user", user.Id);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedAppException("auth.invalid_credentials", "Invalid email or password.");
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = passwordHasher.Hash(command.Password);
        }

        var now = clock.UtcNow;
        var (rawToken, tokenHash) = AuthTokens.Create();
        db.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            Ip = requestContext.Ip,
            UserAgent = requestContext.UserAgent,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now + authOptions.Value.SessionAbsoluteLifetime,
        });

        audit.Add(AuditActions.Login, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);

        return new AuthResult(
            new UserDto(user.Id, user.Email, user.DisplayName, null, user.CreatedAt),
            rawToken,
            AuthTokens.CreateCsrfToken());
    }
}
