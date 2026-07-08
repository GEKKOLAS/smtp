using Amazon.Runtime;
using Amazon.S3;
using Hangfire;
using Hangfire.PostgreSql;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure.Rendering;
using MailTemplateHub.Infrastructure.Audit;
using MailTemplateHub.Infrastructure.Email;
using MailTemplateHub.Infrastructure.Jobs;
using MailTemplateHub.Infrastructure.Persistence;
using MailTemplateHub.Infrastructure.Persistence.Interceptors;
using MailTemplateHub.Infrastructure.Providers;
using MailTemplateHub.Infrastructure.Providers.Google;
using MailTemplateHub.Infrastructure.Providers.Microsoft;
using MailTemplateHub.Infrastructure.Security;
using MailTemplateHub.Infrastructure.Sending;
using MailTemplateHub.Infrastructure.Storage;
using MailTemplateHub.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<TimestampInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options
                .UseNpgsql(database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddSingleton<ISystemEmailSender, LoggingSystemEmailSender>();

        AddOAuth(services);
        AddStorage(services);

        services.AddSingleton<IMjmlCompiler, MjmlNetCompiler>();
        services.AddSingleton<IHtmlSanitizer, GanssHtmlSanitizer>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();

        AddSending(services, configuration);

        return services;
    }

    private static void AddSending(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SendLimitsOptions>().BindConfiguration(SendLimitsOptions.SectionName);
        services.AddOptions<ProviderSendOptions>().BindConfiguration(ProviderSendOptions.SectionName);

        services.AddSingleton<IEmailMessageBuilder, MimeKitEmailMessageBuilder>();
        services.AddHttpClient<GmailEmailProviderClient>();
        services.AddHttpClient<OutlookEmailProviderClient>();
        services.AddScoped<IEmailProviderClient>(sp => sp.GetRequiredService<GmailEmailProviderClient>());
        services.AddScoped<IEmailProviderClient>(sp => sp.GetRequiredService<OutlookEmailProviderClient>());
        services.AddScoped<IEmailProviderClientFactory, EmailProviderClientFactory>();
        services.AddScoped<IEmailSendService, EmailSendService>();
        services.AddScoped<SendEmailJob>();
        services.AddScoped<PromoteScheduledSendsJob>();
        services.AddScoped<RefreshTokensJob>();
        services.AddScoped<CleanupJob>();

        // Hangfire hosts the queue in-process (dev/prod). Tests set RunInProcess=false
        // and substitute a synchronous IBackgroundJobScheduler.
        if (configuration.GetValue("Jobs:RunInProcess", true))
        {
            var connectionString = configuration["Database:ConnectionString"];
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
            services.AddScoped<IBackgroundJobScheduler, HangfireJobScheduler>();
        }
    }

    private static void AddStorage(IServiceCollection services)
    {
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AssetOptions>().BindConfiguration(AssetOptions.SectionName);

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region,
                // Honor the endpoint scheme for presigned URLs (MinIO dev is plain HTTP).
                UseHttp = options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            };
            return new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        });
        services.AddScoped<IObjectStorage, S3ObjectStorage>();
    }

    private static void AddOAuth(IServiceCollection services)
    {
        services.AddOptions<OAuthGeneralOptions>().BindConfiguration(OAuthGeneralOptions.SectionName);
        services.AddOptions<GoogleOAuthOptions>().BindConfiguration(GoogleOAuthOptions.SectionName);
        services.AddOptions<MicrosoftOAuthOptions>().BindConfiguration(MicrosoftOAuthOptions.SectionName);
        services.AddOptions<TokenCryptoOptions>()
            .BindConfiguration(TokenCryptoOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<ITokenCipher, AesGcmTokenCipher>();

        services.AddHttpClient<GoogleOAuthService>();
        services.AddHttpClient<MicrosoftOAuthService>();
        services.AddScoped<IOAuthProviderService>(sp => sp.GetRequiredService<GoogleOAuthService>());
        services.AddScoped<IOAuthProviderService>(sp => sp.GetRequiredService<MicrosoftOAuthService>());
        services.AddScoped<IOAuthProviderResolver, OAuthProviderResolver>();

        services.AddScoped<ITokenRefreshService, TokenRefreshService>();
    }
}
