using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MailTemplateHub.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` at design time; no live database connection is required
/// for generating migrations. Override the connection string with MTH_DESIGNTIME_CONNECTION
/// when scripting against a real server.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MTH_DESIGNTIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=mailtemplatehub;Username=mth;Password=mth_dev_password";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
