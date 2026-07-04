using MailTemplateHub.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MailTemplateHub.Api.Middleware;

/// <summary>
/// Central exception → RFC 7807 mapping. Never leaks stack traces or provider
/// details to clients; 5xx details go to logs only (spec 04-security.md §8).
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = exception switch
        {
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Message, domain.Code),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request cancelled.", "request_cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "internal_error"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions =
                {
                    ["errorCode"] = errorCode,
                    ["traceId"] = httpContext.TraceIdentifier,
                },
            },
        });
    }
}
