using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Providers;

internal sealed class OAuthProviderResolver(IEnumerable<IOAuthProviderService> services) : IOAuthProviderResolver
{
    private readonly Dictionary<EmailProvider, IOAuthProviderService> _byProvider =
        services.ToDictionary(s => s.Provider);

    public IOAuthProviderService For(EmailProvider provider) =>
        _byProvider.TryGetValue(provider, out var service)
            ? service
            : throw new InvalidOperationException($"No OAuth provider service registered for {provider}.");
}
