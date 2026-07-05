using System.Threading.RateLimiting;
using Hangfire;
using MailTemplateHub.Api;
using MailTemplateHub.Api.Auth;
using MailTemplateHub.Api.Middleware;
using MailTemplateHub.Application;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Infrastructure;
using MailTemplateHub.Infrastructure.Jobs;
using MailTemplateHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// --- Auth: cookie sessions + CSRF (spec 04-security.md §1) ---
builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>());
builder.Services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<HttpCurrentUser>());
builder.Services.AddSingleton<AuthCookies>();

builder.Services
    .AddAuthentication(SessionAuthentication.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthentication.Scheme, null);
builder.Services.AddAuthorization();

// --- Rate limiting (spec 04-security.md §6) ---
// Limits are resolved per request via options so configuration overrides
// (tests, per-environment files) are honored without eager reads at startup.
builder.Services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static RateLimitPartition<string> Fixed(
        HttpContext context, Func<RateLimitingOptions, RateLimitingOptions.PolicyOptions> select)
    {
        var policy = select(context.RequestServices
            .GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromMinutes(policy.WindowMinutes),
                QueueLimit = 0,
            });
    }

    options.AddPolicy("auth", context => Fixed(context, o => o.Auth));
    options.AddPolicy("oauth", context => Fixed(context, o => o.Oauth));
    options.AddPolicy("upload", context => Fixed(context, o => o.Upload));
    options.AddPolicy("render", context => Fixed(context, o => o.Render));
    options.AddPolicy("send", context => Fixed(context, o => o.Send));
});

var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("MailTemplateHub.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

var jobsRunInProcess = builder.Configuration.GetValue("Jobs:RunInProcess", true);
if (jobsRunInProcess)
{
    builder.Services.AddHangfireServer(options => options.Queues = ["default"]);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CsrfMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/healthz");
app.MapControllers();

// Dev/test startup gate; production applies migrations in the deploy pipeline.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    if (database.ApplyMigrationsOnStartup)
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}

// Recurring background jobs (spec 10-jobs.md); Hangfire tables are created by the server.
if (jobsRunInProcess)
{
    RecurringJob.AddOrUpdate<PromoteScheduledSendsJob>(
        "promote-scheduled", job => job.RunAsync(CancellationToken.None), "* * * * *");
}

await app.RunAsync();

public partial class Program; // exposes the entry point to WebApplicationFactory
