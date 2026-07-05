using System.Security.Claims;
using MailTemplateHub.Application.Abstractions;

namespace MailTemplateHub.Api.Auth;

/// <summary>ICurrentUser/IRequestContext backed by the current HttpContext.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser, IRequestContext
{
    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context.");

    public Guid UserId =>
        Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Caller is not authenticated.");

    public Guid SessionId =>
        Guid.TryParse(Context.User.FindFirstValue(SessionAuthentication.SessionIdClaim), out var id)
            ? id
            : throw new InvalidOperationException("Caller is not authenticated.");

    // Null in background jobs (no HTTP context), so audit writes still work there.
    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(value) ? null : value[..Math.Min(value.Length, 500)];
        }
    }
}
