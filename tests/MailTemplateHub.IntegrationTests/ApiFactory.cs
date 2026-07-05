using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Testcontainers.Minio;
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

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:latest")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    private const string PublicBucket = "mth-public";
    private const string PrivateBucket = "mth-private";
    private const string SnapshotsBucket = "mth-snapshots";

    // Deterministic 32-byte KEK for the test process only.
    private static readonly string TestKek = Convert.ToBase64String(
        SHA256.HashData("mth-integration-test-kek"u8.ToArray()));

    public RecordingEmailSender EmailSender { get; } = new();
    public StubOAuthHandler OAuth { get; } = new();
    public FakeEmailProviderClient Provider { get; } = new();

    // Build an explicit http URL; a bare host:port makes the S3 SDK assume https,
    // which fails against MinIO's plain-HTTP endpoint.
    private string MinioEndpoint => $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());
        await CreateBucketsAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
    }

    private async Task CreateBucketsAsync()
    {
        using var s3 = CreateS3Client();
        foreach (var bucket in new[] { PrivateBucket, PublicBucket, SnapshotsBucket })
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        }
    }

    public IAmazonS3 CreateS3Client() => new AmazonS3Client(
        new BasicAWSCredentials("minioadmin", "minioadmin"),
        new AmazonS3Config { ServiceURL = MinioEndpoint, ForcePathStyle = true, UseHttp = true });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Read at build time in Program.cs, so it must be set as a host setting
        // (the in-memory config below is only merged for post-build option reads).
        builder.UseSetting("Jobs:RunInProcess", "false");

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

                ["Storage:ServiceUrl"] = MinioEndpoint,
                ["Storage:AccessKey"] = "minioadmin",
                ["Storage:SecretKey"] = "minioadmin",
                ["Storage:PublicBaseUrl"] = $"{MinioEndpoint}/{PublicBucket}",

                // Run send jobs synchronously in-process; no Hangfire server.
                ["Jobs:RunInProcess"] = "false",
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

            // Send through the in-process fake provider and run jobs synchronously.
            services.RemoveAll<IEmailProviderClientFactory>();
            services.AddSingleton(Provider);
            services.AddSingleton<IEmailProviderClientFactory>(new FakeProviderClientFactory(Provider));
            services.RemoveAll<IBackgroundJobScheduler>();
            services.AddScoped<IBackgroundJobScheduler, SynchronousJobScheduler>();
        });
    }
}
