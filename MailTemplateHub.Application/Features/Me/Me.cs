using FluentValidation;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Auth;
using MailTemplateHub.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Me;

public sealed record UpdateProfileCommand(string DisplayName);

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class MeHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IValidator<UpdateProfileCommand> profileValidator,
    IValidator<ChangePasswordCommand> passwordValidator)
{
    public async Task<UserDto> GetAsync(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        return new UserDto(user.Id, user.Email, user.DisplayName, null, user.CreatedAt);
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileCommand command, CancellationToken ct)
    {
        await profileValidator.ValidateAndThrowAsync(command, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        user.DisplayName = command.DisplayName;
        await db.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.Email, user.DisplayName, null, user.CreatedAt);
    }

    public async Task ChangePasswordAsync(ChangePasswordCommand command, CancellationToken ct)
    {
        await passwordValidator.ValidateAndThrowAsync(command, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAppException("auth.wrong_password", "Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.Hash(command.NewPassword);

        // Keep the session that made the change; end every other one.
        await db.UserSessions
            .Where(s => s.UserId == currentUser.UserId && s.Id != currentUser.SessionId)
            .ExecuteDeleteAsync(ct);

        audit.Add(AuditActions.PasswordChanged, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);
    }
}
