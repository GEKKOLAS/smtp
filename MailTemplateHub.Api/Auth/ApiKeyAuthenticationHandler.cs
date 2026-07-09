using System.Security.Claims;
using System.Text.Encodings.Web;
using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Features.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailTemplateHub.Api.Auth;

public static class ApiKeyAuthentication
{
    public const string Scheme = "ApiKey";
}

/// <summary>
/// Authenticates programmatic requests via <c>Authorization: Bearer mth_…</c>
/// (spec: automation/n8n access). Only the SHA-256 of the key is stored; last-used
/// is refreshed at most every few minutes.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IClock clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private static readonly TimeSpan LastUsedRefreshInterval = TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValueTryParse(out var token) || !ApiKeyGenerator.LooksLikeKey(token))
        {
            return AuthenticateResult.NoResult();
        }

        var db = Context.RequestServices.GetRequiredService<IAppDbContext>();
        var hash = ApiKeyGenerator.Hash(token);
        var key = await db.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash, Context.RequestAborted);

        var now = clock.UtcNow;
        if (key?.User is null || key.User.DeletedAt is not null || !key.IsUsable(now))
        {
            return AuthenticateResult.NoResult();
        }

        if (key.LastUsedAt is null || now - key.LastUsedAt > LastUsedRefreshInterval)
        {
            key.LastUsedAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, key.UserId.ToString()),
                new Claim(ClaimTypes.Name, key.User.DisplayName),
                new Claim("mth:auth", "api_key"),
            ],
            ApiKeyAuthentication.Scheme);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthentication.Scheme));
    }

    private bool AuthenticationHeaderValueTryParse(out string token)
    {
        token = string.Empty;
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        token = header["Bearer ".Length..].Trim();
        return token.Length > 0;
    }
}
