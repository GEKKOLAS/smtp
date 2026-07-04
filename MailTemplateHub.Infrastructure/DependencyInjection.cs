using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure.Audit;
using MailTemplateHub.Infrastructure.Email;
using MailTemplateHub.Infrastructure.Persistence;
using MailTemplateHub.Infrastructure.Persistence.Interceptors;
using MailTemplateHub.Infrastructure.Providers;
using MailTemplateHub.Infrastructure.Providers.Google;
using MailTemplateHub.Infrastructure.Providers.Microsoft;
using MailTemplateHub.Infrastructure.Security;
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

        return services;
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
