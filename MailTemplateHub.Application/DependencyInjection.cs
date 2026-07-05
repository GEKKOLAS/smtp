using FluentValidation;
using MailTemplateHub.Application.Features.Accounts;
using MailTemplateHub.Application.Features.Assets;
using MailTemplateHub.Application.Features.Auth;
using MailTemplateHub.Application.Features.Me;
using MailTemplateHub.Application.Features.Rendering;
using MailTemplateHub.Application.Features.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<SessionsHandler>();
        services.AddScoped<PasswordResetHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<ConnectStartHandler>();
        services.AddScoped<OAuthCallbackHandler>();
        services.AddScoped<AccountsHandler>();
        services.AddScoped<RequestUploadHandler>();
        services.AddScoped<CompleteUploadHandler>();
        services.AddScoped<AssetsHandler>();
        services.AddScoped<TemplateVersionFactory>();
        services.AddScoped<TemplatesHandler>();
        services.AddScoped<TemplateVersionsHandler>();
        services.AddScoped<RenderHandler>();

        return services;
    }
}
