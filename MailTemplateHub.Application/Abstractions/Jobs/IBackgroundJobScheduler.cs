namespace MailTemplateHub.Application.Abstractions.Jobs;

/// <summary>
/// Enqueues background work without the Application layer referencing Hangfire
/// (spec 03 §4, 10-jobs.md). A test double runs jobs synchronously.
/// </summary>
public interface IBackgroundJobScheduler
{
    void EnqueueSend(Guid sendJobId);

    void ScheduleSendRetry(Guid sendJobId, TimeSpan delay);
}

/// <summary>
/// Processes one send job end to end (render → build MIME → provider send →
/// record outcomes → finalize). Implemented in Infrastructure and invoked by the
/// Hangfire job so orchestration stays testable in isolation.
/// </summary>
public interface IEmailSendService
{
    Task ProcessJobAsync(Guid sendJobId, CancellationToken ct);
}
