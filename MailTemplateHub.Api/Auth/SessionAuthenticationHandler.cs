using System.Security.Claims;
using System.Text.Encodings.Web;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Application.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Api.Auth;

public static class SessionAuthentication
{
    public const string Scheme = "Session";
    public const string SessionIdClaim = "mth:session_id";
}

/// <summary>
/// Authenticates requests from the session cookie: cookie carries the raw token,
/// the database stores its SHA-256 (spec 04-security.md §1). LastSeenAt is
/// refreshed at most every five minutes to keep reads cheap.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions,
    IClock clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private static readonly TimeSpan LastSeenRefreshInterval = TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var auth = authOptions.Value;
        var rawToken = Request.Cookies[auth.SessionCookieName];
        if (string.IsNullOrEmpty(rawToken)) return AuthenticateResult.NoResult();

        var db = Context.RequestServices.GetRequiredService<IAppDbContext>();
        var tokenHash = AuthTokens.HashToken(rawToken);
        var session = await db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, Context.RequestAborted);

        var now = clock.UtcNow;
        if (session?.User is null || !session.IsActive(now, auth.SessionIdleTimeout))
        {
            return AuthenticateResult.NoResult();
        }

        if (now - session.LastSeenAt > LastSeenRefreshInterval)
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim(ClaimTypes.Name, session.User.DisplayName),
                new Claim(SessionAuthentication.SessionIdClaim, session.Id.ToString()),
            ],
            SessionAuthentication.Scheme);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SessionAuthentication.Scheme));
    }
}
