using Hangfire;
using MailTemplateHub.Application.Abstractions.Jobs;

namespace MailTemplateHub.Infrastructure.Jobs;

internal sealed class HangfireJobScheduler(IBackgroundJobClient client) : IBackgroundJobScheduler
{
    public void EnqueueSend(Guid sendJobId) =>
        client.Enqueue<SendEmailJob>(job => job.RunAsync(sendJobId));

    public void ScheduleSendRetry(Guid sendJobId, TimeSpan delay) =>
        client.Schedule<SendEmailJob>(job => job.RunAsync(sendJobId), delay);
}
