using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Auth;

public sealed class SessionsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit)
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(CancellationToken ct)
    {
        var sessions = await db.UserSessions
            .Where(s => s.UserId == currentUser.UserId)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(ct);

        return sessions
            .Select(s => new SessionDto(s.Id, s.Ip, s.UserAgent, s.CreatedAt, s.LastSeenAt, s.Id == currentUser.SessionId))
            .ToList();
    }

    /// <summary>Logout: deletes the current session.</summary>
    public async Task LogoutAsync(CancellationToken ct)
    {
        await db.UserSessions
            .Where(s => s.Id == currentUser.SessionId && s.UserId == currentUser.UserId)
            .ExecuteDeleteAsync(ct);

        audit.Add(AuditActions.Logout, currentUser.UserId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Logout everywhere: deletes all sessions for the user.</summary>
    public async Task LogoutAllAsync(CancellationToken ct)
    {
        await db.UserSessions
            .Where(s => s.UserId == currentUser.UserId)
            .ExecuteDeleteAsync(ct);

        audit.Add(AuditActions.Logout, currentUser.UserId, metadata: new { everywhere = true });
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        var deleted = await db.UserSessions
            .Where(s => s.Id == sessionId && s.UserId == currentUser.UserId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException();

        audit.Add(AuditActions.SessionRevoked, currentUser.UserId, "user_session", sessionId);
        await db.SaveChangesAsync(ct);
    }
}
