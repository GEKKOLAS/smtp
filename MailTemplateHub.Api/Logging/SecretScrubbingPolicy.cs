using Serilog.Core;
using Serilog.Events;

namespace MailTemplateHub.Api.Logging;

/// <summary>
/// Redacts secret-bearing properties from structured logs (spec 04-security.md §2, §8):
/// tokens, passwords, and Authorization headers never reach the sinks.
/// </summary>
public sealed class SecretScrubbingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token", "refresh_token", "accessToken", "refreshToken", "id_token", "idToken",
        "Authorization", "password", "newPassword", "currentPassword", "client_secret",
        "clientSecret", "pkceVerifier", "token", "raw",
    };

    private const string Redacted = "***redacted***";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
        {
            if (SecretKeys.Contains(property.Key))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(Redacted)));
            }
        }
    }
}
