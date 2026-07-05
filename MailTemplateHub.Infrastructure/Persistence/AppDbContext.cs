using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ConnectedEmailAccount> ConnectedEmailAccounts => Set<ConnectedEmailAccount>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();
    public DbSet<EmailProviderEvent> EmailProviderEvents => Set<EmailProviderEvent>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailTemplateVersion> EmailTemplateVersions => Set<EmailTemplateVersion>();
    public DbSet<TemplateAsset> TemplateAssets => Set<TemplateAsset>();
    public DbSet<EmailSendJob> EmailSendJobs => Set<EmailSendJob>();
    public DbSet<EmailSendRecipient> EmailSendRecipients => Set<EmailSendRecipient>();
    public DbSet<EmailSendAttachment> EmailSendAttachments => Set<EmailSendAttachment>();

    public async Task<IAsyncDisposable> AcquireAdvisoryLockAsync(long key, CancellationToken cancellationToken)
    {
        // Hold the connection open so the session-level lock persists across the
        // subsequent re-read + refresh + save (spec 04 §2).
        await Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({key})", cancellationToken);
        }
        catch
        {
            await Database.CloseConnectionAsync();
            throw;
        }
        return new AdvisoryLockHandle(this, key);
    }

    private sealed class AdvisoryLockHandle(AppDbContext context, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({key})", CancellationToken.None);
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext gives case-insensitive email/name columns (spec 05-database.md).
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
