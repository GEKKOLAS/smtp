using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Providers;

internal sealed class EmailProviderClientFactory(IEnumerable<IEmailProviderClient> clients)
    : IEmailProviderClientFactory
{
    private readonly Dictionary<EmailProvider, IEmailProviderClient> _byProvider =
        clients.ToDictionary(c => c.Provider);

    public IEmailProviderClient For(EmailProvider provider) =>
        _byProvider.TryGetValue(provider, out var client)
            ? client
            : throw new InvalidOperationException($"No email provider client registered for {provider}.");
}
