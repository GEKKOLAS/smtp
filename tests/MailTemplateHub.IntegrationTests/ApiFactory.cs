using System.Security.Cryptography;
using MailTemplateHub.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
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

    // Deterministic 32-byte KEK for the test process only.
    private static readonly string TestKek = Convert.ToBase64String(
        SHA256.HashData("mth-integration-test-kek"u8.ToArray()));

    public RecordingEmailSender EmailSender { get; } = new();
    public StubOAuthHandler OAuth { get; } = new();

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
                // Generous default so unrelated tests never trip the limiter;
                // the rate-limit test lowers it via WithWebHostBuilder.
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                ["RateLimiting:Oauth:PermitLimit"] = "1000",

                ["TokenCrypto:ActiveKekVersion"] = "1",
                ["TokenCrypto:Keys:1"] = TestKek,

                ["OAuth:RedirectBaseUrl"] = "http://localhost:5001",
                ["OAuth:FrontendBaseUrl"] = "http://localhost:3000",
                ["OAuth:Google:ClientId"] = "test-google-client",
                ["OAuth:Google:ClientSecret"] = "test-google-secret",
                ["OAuth:Google:AuthorizationEndpoint"] = "https://oauth.test/google/auth",
                ["OAuth:Google:TokenEndpoint"] = "https://oauth.test/google/token",
                ["OAuth:Google:UserInfoEndpoint"] = "https://oauth.test/google/userinfo",
                ["OAuth:Google:RevokeEndpoint"] = "https://oauth.test/google/revoke",
                ["OAuth:Google:Scopes:0"] = "openid",
                ["OAuth:Google:Scopes:1"] = "email",
                ["OAuth:Google:Scopes:2"] = "profile",
                ["OAuth:Google:Scopes:3"] = "https://www.googleapis.com/auth/gmail.send",

                ["OAuth:Microsoft:ClientId"] = "test-ms-client",
                ["OAuth:Microsoft:ClientSecret"] = "test-ms-secret",
                ["OAuth:Microsoft:AuthorizationEndpoint"] = "https://oauth.test/ms/auth",
                ["OAuth:Microsoft:TokenEndpoint"] = "https://oauth.test/ms/token",
                ["OAuth:Microsoft:UserInfoEndpoint"] = "https://oauth.test/ms/me",
                ["OAuth:Microsoft:Scopes:0"] = "openid",
                ["OAuth:Microsoft:Scopes:1"] = "offline_access",
                ["OAuth:Microsoft:Scopes:2"] = "User.Read",
                ["OAuth:Microsoft:Scopes:3"] = "Mail.Send",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISystemEmailSender>();
            services.AddSingleton<ISystemEmailSender>(EmailSender);

            // Route the provider OAuth HttpClients through the in-process stub.
            foreach (var clientName in new[] { "GoogleOAuthService", "MicrosoftOAuthService" })
            {
                services.Configure<HttpClientFactoryOptions>(clientName, options =>
                    options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = OAuth));
            }
        });
    }
}
