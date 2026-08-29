using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Providers;

internal sealed class InboxProviderClientFactory(IEnumerable<IInboxProviderClient> clients)
    : IInboxProviderClientFactory
{
    private readonly Dictionary<EmailProvider, IInboxProviderClient> _byProvider =
        clients.ToDictionary(c => c.Provider);

    public IInboxProviderClient For(EmailProvider provider) =>
        _byProvider.TryGetValue(provider, out var client)
            ? client
            : throw new InvalidOperationException($"No inbox provider client registered for {provider}.");
}
