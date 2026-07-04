using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Auth;

public sealed record RegisterCommand(string Email, string Password, string DisplayName);

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

public sealed class RegisterHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IRequestContext requestContext,
    IOptions<AuthOptions> authOptions,
    IValidator<RegisterCommand> validator,
    IClock clock)
{
    public async Task<AuthResult> HandleAsync(RegisterCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        // Hash before the existence check so duplicate and fresh registrations
        // take comparable time (anti-enumeration, spec 04 §1).
        var passwordHash = passwordHasher.Hash(command.Password);

        var exists = await db.Users.AnyAsync(u => u.Email == command.Email, ct);
        if (exists)
        {
            // Same response shape, no session cookie — the caller cannot tell
            // whether the address was already registered.
            var decoy = new UserDto(Guid.CreateVersion7(), command.Email, command.DisplayName, null, clock.UtcNow);
            return new AuthResult(decoy, SessionToken: null, CsrfToken: null);
        }

        var now = clock.UtcNow;
        var user = new User
        {
            Email = command.Email,
            PasswordHash = passwordHash,
            DisplayName = command.DisplayName,
        };
        db.Users.Add(user);

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

        audit.Add(AuditActions.Register, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);

        return new AuthResult(
            new UserDto(user.Id, user.Email, user.DisplayName, null, now),
            rawToken,
            AuthTokens.CreateCsrfToken());
    }
}
