using System.Collections.Concurrent;
using MailTemplateHub.Application.Abstractions;

namespace MailTemplateHub.IntegrationTests;

/// <summary>Captures reset tokens instead of sending mail so tests can complete the flow.</summary>
public sealed class RecordingEmailSender : ISystemEmailSender
{
    public ConcurrentQueue<(string Email, string Token)> Sent { get; } = new();

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        Sent.Enqueue((email, token));
        return Task.CompletedTask;
    }
}
