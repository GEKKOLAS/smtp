using MailTemplateHub.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace MailTemplateHub.IntegrationTests;

/// <summary>Smoke tests that the recurring jobs' EF queries translate and run against the real schema.</summary>
public class BackgroundJobsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Cleanup_job_runs_without_error()
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<CleanupJob>();
        await job.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Refresh_tokens_job_runs_without_error()
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RefreshTokensJob>();
        await job.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Promote_scheduled_job_runs_without_error()
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PromoteScheduledSendsJob>();
        await job.RunAsync(CancellationToken.None);
    }
}
