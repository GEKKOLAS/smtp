using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Abstractions;

/// <summary>
/// Persistence port for Application handlers, keeping them free of a direct
/// Infrastructure reference. DbSets grow as entities land per phase.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
