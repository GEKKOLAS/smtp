using System.Text;

namespace MailTemplateHub.Infrastructure.Providers;

internal static class QueryHelpers
{
    /// <summary>Appends URL-encoded query parameters to a base URL.</summary>
    public static string AddQuery(string url, IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var builder = new StringBuilder(url);
        var first = !url.Contains('?', StringComparison.Ordinal);
        foreach (var (key, value) in parameters)
        {
            if (value is null) continue;
            builder.Append(first ? '?' : '&');
            first = false;
            builder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }
        return builder.ToString();
    }
}
