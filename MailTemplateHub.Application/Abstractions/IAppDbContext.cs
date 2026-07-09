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
    DbSet<ConnectedEmailAccount> ConnectedEmailAccounts { get; }
    DbSet<OAuthToken> OAuthTokens { get; }
    DbSet<OAuthState> OAuthStates { get; }
    DbSet<EmailProviderEvent> EmailProviderEvents { get; }
    DbSet<Asset> Assets { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailTemplateVersion> EmailTemplateVersions { get; }
    DbSet<TemplateAsset> TemplateAssets { get; }
    DbSet<EmailSendJob> EmailSendJobs { get; }
    DbSet<EmailSendRecipient> EmailSendRecipients { get; }
    DbSet<EmailSendAttachment> EmailSendAttachments { get; }
    DbSet<ApiKey> ApiKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Postgres session-level advisory lock keyed on an account, serializing
    /// concurrent token refreshes across instances (spec 04 §2). The returned
    /// handle keeps the connection open and releases the lock when disposed.
    /// </summary>
    Task<IAsyncDisposable> AcquireAdvisoryLockAsync(long key, CancellationToken cancellationToken);
}
