using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Common;

/// <summary>
/// Scope names the app cares about beyond the connect-time required set, and the
/// comparison rule shared with the OAuth callback: providers sometimes echo a
/// scope as a fully-qualified resource URI ("https://graph.microsoft.com/Mail.Send")
/// instead of the bare name requested ("Mail.Send"), so compare by the last path
/// segment either way.
/// </summary>
public static class ProviderScopes
{
    public const string GmailRead = "gmail.readonly";
    public const string OutlookRead = "Mail.Read";

    public static string InboxReadScope(EmailProvider provider) => provider switch
    {
        EmailProvider.Gmail => GmailRead,
        _ => OutlookRead,
    };

    public static bool HasScope(IEnumerable<string> granted, string scope)
    {
        var wanted = Normalize(scope);
        return granted.Any(g => string.Equals(Normalize(g), wanted, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string scope)
    {
        var slash = scope.LastIndexOf('/');
        return slash >= 0 ? scope[(slash + 1)..] : scope;
    }
}
