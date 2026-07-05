using Amazon.Runtime;
using Amazon.S3;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Abstractions.Storage;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure.Rendering;
using MailTemplateHub.Infrastructure.Audit;
using MailTemplateHub.Infrastructure.Email;
using MailTemplateHub.Infrastructure.Persistence;
using MailTemplateHub.Infrastructure.Persistence.Interceptors;
using MailTemplateHub.Infrastructure.Providers;
using MailTemplateHub.Infrastructure.Providers.Google;
using MailTemplateHub.Infrastructure.Providers.Microsoft;
using MailTemplateHub.Infrastructure.Security;
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

        return services;
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
