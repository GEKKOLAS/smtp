using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Application.Features.Auth;

public sealed record ForgotPasswordCommand(string Email);

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed record ResetPasswordCommand(string Token, string NewPassword);

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class PasswordResetHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ISystemEmailSender emailSender,
    IAuditWriter audit,
    IOptions<AuthOptions> authOptions,
    IValidator<ForgotPasswordCommand> forgotValidator,
    IValidator<ResetPasswordCommand> resetValidator,
    IClock clock)
{
    /// <summary>Always succeeds from the caller's perspective (202), known email or not.</summary>
    public async Task RequestAsync(ForgotPasswordCommand command, CancellationToken ct)
    {
        await forgotValidator.ValidateAndThrowAsync(command, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == command.Email, ct);
        if (user is null) return;

        var now = clock.UtcNow;
        var (rawToken, tokenHash) = AuthTokens.Create();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + TimeSpan.FromMinutes(authOptions.Value.ResetTokenMinutes),
        });
        await db.SaveChangesAsync(ct);

        await emailSender.SendPasswordResetAsync(user.Email, rawToken, ct);
    }

    public async Task ResetAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        await resetValidator.ValidateAndThrowAsync(command, ct);

        var tokenHash = AuthTokens.HashToken(command.Token);
        var token = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        var now = clock.UtcNow;
        if (token?.User is null || !token.IsUsable(now))
        {
            throw new UnauthorizedAppException("auth.invalid_token", "The reset link is invalid or has expired.");
        }

        token.User.PasswordHash = passwordHasher.Hash(command.NewPassword);
        token.UsedAt = now;

        // A reset proves control of the mailbox, not of existing sessions.
        await db.UserSessions.Where(s => s.UserId == token.UserId).ExecuteDeleteAsync(ct);

        audit.Add(AuditActions.PasswordReset, token.UserId, "user", token.UserId);
        await db.SaveChangesAsync(ct);
    }
}
