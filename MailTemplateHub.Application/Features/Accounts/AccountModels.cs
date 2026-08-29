using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Application.Features.Accounts;

/// <summary>Connected-account view. Never carries token material (spec 06-api.md §4).</summary>
public sealed record AccountDto(
    Guid Id,
    string Provider,
    string EmailAddress,
    string? DisplayName,
    string State,
    string? StateReason,
    bool IsDefault,
    IReadOnlyList<string> GrantedScopes,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastUsedAt,
    // Inbox reading is a separate consent from sending, so accounts connected
    // before it existed (or where the user declined it) report false here.
    bool CanReadInbox)
{
    public static AccountDto From(ConnectedEmailAccount a) => new(
        a.Id,
        a.Provider.ToString().ToLowerInvariant(),
        a.EmailAddress,
        a.DisplayName,
        a.State switch
        {
            AccountState.Active => "active",
            AccountState.NeedsReconnect => "needs_reconnect",
            _ => "revoked",
        },
        a.StateReason,
        a.IsDefault,
        a.GrantedScopes,
        a.ConnectedAt,
        a.LastUsedAt,
        a.State == AccountState.Active
        && Common.ProviderScopes.HasScope(a.GrantedScopes, Common.ProviderScopes.InboxReadScope(a.Provider)));
}
