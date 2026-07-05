using System.Collections.Concurrent;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Jobs;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// In-process provider double: records sent messages and applies a configurable
/// behavior so tests can script success, transient, or permanent failures.
/// </summary>
public sealed class FakeEmailProviderClient : IEmailProviderClient
{
    public EmailProvider Provider { get; set; } = EmailProvider.Gmail;
    public TimeSpan MinSendInterval => TimeSpan.Zero;

    public ConcurrentQueue<OutgoingEmail> Sent { get; } = new();

    /// <summary>Called per recipient; return a result or throw ProviderSendException.</summary>
    public Func<OutgoingEmail, ProviderSendResult> Behavior { get; set; } =
        _ => new ProviderSendResult("msg-" + Guid.NewGuid().ToString("N"), null, "sent");

    public Task<ProviderSendResult> SendAsync(
        ConnectedAccountContext account, OutgoingEmail email, CancellationToken ct)
    {
        var result = Behavior(email); // may throw
        Sent.Enqueue(email);
        return Task.FromResult(result);
    }

    public void Reset()
    {
        Sent.Clear();
        Behavior = _ => new ProviderSendResult("msg-" + Guid.NewGuid().ToString("N"), null, "sent");
    }
}

public sealed class FakeProviderClientFactory(FakeEmailProviderClient client) : IEmailProviderClientFactory
{
    public IEmailProviderClient For(EmailProvider provider) => client;
}

/// <summary>
/// Runs send jobs synchronously in a fresh scope so POST /sends completes after
/// the send. Retries are recorded but not auto-run (tests assert the Retrying
/// state or drive the manual retry endpoint).
/// </summary>
public sealed class SynchronousJobScheduler(IServiceScopeFactory scopeFactory) : IBackgroundJobScheduler
{
    public ConcurrentQueue<(Guid JobId, TimeSpan Delay)> ScheduledRetries { get; } = new();

    public void EnqueueSend(Guid sendJobId)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailSendService>();
        service.ProcessJobAsync(sendJobId, CancellationToken.None).GetAwaiter().GetResult();
    }

    public void ScheduleSendRetry(Guid sendJobId, TimeSpan delay) =>
        ScheduledRetries.Enqueue((sendJobId, delay));
}
