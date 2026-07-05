using Hangfire;
using MailTemplateHub.Application.Abstractions.Jobs;

namespace MailTemplateHub.Infrastructure.Jobs;

/// <summary>
/// Thin Hangfire wrapper over the send orchestration. Hangfire's own retries are
/// disabled — our per-recipient state machine owns retries so backoff survives
/// restarts (spec 10-jobs.md J1).
/// </summary>
public sealed class SendEmailJob(IEmailSendService sendService)
{
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(Guid sendJobId) => sendService.ProcessJobAsync(sendJobId, CancellationToken.None);
}
