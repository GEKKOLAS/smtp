using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using MailTemplateHub.Domain.Errors;

namespace MailTemplateHub.UnitTests.Domain;

public class SendJobStateMachineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static EmailSendJob Job(params RecipientStatus[] statuses)
    {
        var job = new EmailSendJob
        {
            UserId = Guid.NewGuid(),
            ConnectedEmailAccountId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            Status = SendJobStatus.Queued,
            SubjectSnapshot = "s",
        };
        foreach (var status in statuses)
        {
            job.Recipients.Add(new EmailSendRecipient
            {
                SendJobId = job.Id,
                EmailAddress = $"r{Guid.NewGuid():N}@x.test",
                Status = status,
            });
        }
        return job;
    }

    [Fact]
    public void MarkSending_from_queued_is_allowed()
    {
        var job = Job(RecipientStatus.Pending);
        job.MarkSending(Now);
        Assert.Equal(SendJobStatus.Sending, job.Status);
    }

    [Fact]
    public void MarkSending_from_sent_throws()
    {
        var job = Job(RecipientStatus.Sent);
        job.Status = SendJobStatus.Sent;
        Assert.Throws<DomainException>(() => job.MarkSending(Now));
    }

    [Fact]
    public void Finalize_all_sent_is_sent()
    {
        var job = Job(RecipientStatus.Sent, RecipientStatus.Sent);
        job.Finalize(Now);
        Assert.Equal(SendJobStatus.Sent, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void Finalize_mixed_is_partially_failed()
    {
        var job = Job(RecipientStatus.Sent, RecipientStatus.Failed);
        job.Finalize(Now);
        Assert.Equal(SendJobStatus.PartiallyFailed, job.Status);
    }

    [Fact]
    public void Finalize_all_failed_is_failed()
    {
        var job = Job(RecipientStatus.Failed, RecipientStatus.Failed);
        job.Finalize(Now);
        Assert.Equal(SendJobStatus.Failed, job.Status);
    }

    [Fact]
    public void Finalize_with_pending_is_retrying()
    {
        var job = Job(RecipientStatus.Sent);
        job.Recipients.Add(new EmailSendRecipient
        {
            SendJobId = job.Id, EmailAddress = "p@x.test",
            Status = RecipientStatus.Pending, NextAttemptAt = Now.AddMinutes(1),
        });
        job.Finalize(Now);
        Assert.Equal(SendJobStatus.Retrying, job.Status);
        Assert.Equal(Now.AddMinutes(1), job.NextAttemptAt);
    }

    [Fact]
    public void ScheduleRetry_fails_permanently_after_max_attempts()
    {
        var recipient = new EmailSendRecipient { SendJobId = Guid.NewGuid(), EmailAddress = "r@x.test" };
        recipient.AttemptCount = 4;
        recipient.ScheduleRetry(TimeSpan.FromMinutes(1), maxAttempts: 5, "transient", "temp", Now);

        Assert.Equal(RecipientStatus.Failed, recipient.Status);
        Assert.Equal("retries_exhausted", recipient.FailureCode);
    }

    [Fact]
    public void ScheduleRetry_reschedules_below_max()
    {
        var recipient = new EmailSendRecipient { SendJobId = Guid.NewGuid(), EmailAddress = "r@x.test" };
        recipient.ScheduleRetry(TimeSpan.FromMinutes(2), maxAttempts: 5, "transient", "temp", Now);

        Assert.Equal(RecipientStatus.Pending, recipient.Status);
        Assert.Equal(Now.AddMinutes(2), recipient.NextAttemptAt);
    }

    [Fact]
    public void Cancel_cancels_pending_recipients()
    {
        var job = Job(RecipientStatus.Sent, RecipientStatus.Pending);
        job.Status = SendJobStatus.Sending;
        job.Cancel(Now);

        Assert.Equal(SendJobStatus.Cancelled, job.Status);
        Assert.Single(job.Recipients, r => r.Status == RecipientStatus.Cancelled);
        Assert.Single(job.Recipients, r => r.Status == RecipientStatus.Sent); // already-sent kept
    }
}
