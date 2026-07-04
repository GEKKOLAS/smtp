using MailTemplateHub.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext gives case-insensitive email/name columns (spec 05-database.md).
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
