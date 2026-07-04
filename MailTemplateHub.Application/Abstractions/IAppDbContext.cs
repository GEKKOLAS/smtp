namespace MailTemplateHub.Application.Abstractions;

/// <summary>
/// Persistence port for Application handlers. DbSet properties are added here as
/// entities land (Phase 1+), keeping handlers free of a direct Infrastructure reference.
/// </summary>
public interface IAppDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
