using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL 16 container.
/// Migrations run via the startup gate (Database:ApplyMigrationsOnStartup).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Testcontainers is pinned to 3.x until local Docker Desktop supports API >= 1.44
    // (4.x requires it). Bump to 4.x after upgrading Docker Desktop.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("mailtemplatehub_test")
        .WithUsername("mth")
        .WithPassword("mth_test_password")
        .Build();

    Task IAsyncLifetime.InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),
                ["Database:ApplyMigrationsOnStartup"] = "true",
            }));
    }
}
