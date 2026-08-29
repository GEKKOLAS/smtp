using MailTemplateHub.Application.Abstractions;
using MailTemplateHub.Application.Abstractions.Email;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Abstractions.Rendering;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Audit;
using MailTemplateHub.Domain.Entities;
using MailTemplateHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailTemplateHub.Application.Features.Inbox;

/// <summary>
/// Reads a connected mailbox so a template can be sent as a reply in an existing
/// thread. Read access is a separate consent from sending: an account connected
/// before this feature (or one where the user declined the read scope) keeps
/// working for sends and surfaces <c>inbox.scope_missing</c> here.
/// </summary>
public sealed class InboxHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IInboxProviderClientFactory inboxClients,
    ITokenRefreshService tokenRefresh,
    IHtmlSanitizer sanitizer,
    IAuditWriter audit,
    IClock clock)
{
    private const int MaxPageSize = 50;

    public async Task<InboxPageDto> ListAsync(InboxListQuery query, CancellationToken ct)
    {
        var (account, context) = await ResolveAsync(query.AccountId, ct);
        var client = inboxClients.For(account.Provider);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, MaxPageSize);

        var page = await CallProviderAsync(
            account, () => client.ListInboxAsync(context, query.PageToken, query.Search, pageSize, ct), ct);

        account.LastUsedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        return new InboxPageDto([.. page.Items.Select(InboxMessageDto.From)], page.NextPageToken);
    }

    public async Task<InboxMessageDetailDto> GetAsync(Guid accountId, string messageId, CancellationToken ct)
    {
        var message = await LoadMessageAsync(accountId, messageId, ct);

        audit.Add(AuditActions.InboxMessageRead, currentUser.UserId, "connected_email_account", accountId,
            new { messageId });
        await db.SaveChangesAsync(ct);

        return new InboxMessageDetailDto(
            message.Id,
            message.ThreadId,
            message.FromEmail,
            message.FromName,
            message.To,
            message.Subject,
            message.ReceivedAt,
            // Sender-controlled HTML: sanitize before it reaches the reader pane.
            message.HtmlBody is { } html ? sanitizer.Sanitize(html) : null,
            message.TextBody,
            ReplySubject(message.Subject));
    }

    /// <summary>
    /// Fetches the message a send is replying to. The caller only ever supplies a
    /// message id — the threading identifiers come from the provider, so a client
    /// cannot inject arbitrary In-Reply-To/References values.
    /// </summary>
    public async Task<InboxMessageDetail> LoadMessageAsync(
        Guid accountId, string messageId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(messageId)) throw new NotFoundException();

        var (account, context) = await ResolveAsync(accountId, ct);
        var client = inboxClients.For(account.Provider);
        return await CallProviderAsync(account, () => client.GetMessageAsync(context, messageId, ct), ct);
    }

    /// <summary>"Re: " prefix, without stacking one onto a subject that already has it.</summary>
    public static string ReplySubject(string subject) =>
        subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase) ? subject : $"Re: {subject}";

    private async Task<(ConnectedEmailAccount Account, ConnectedAccountContext Context)> ResolveAsync(
        Guid accountId, CancellationToken ct)
    {
        var account = await db.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        if (account.State != AccountState.Active)
        {
            throw new ConflictException("inbox.account_needs_reconnect", "This account needs to be reconnected.");
        }
        if (!ProviderScopes.HasScope(account.GrantedScopes, ProviderScopes.InboxReadScope(account.Provider)))
        {
            throw new ConflictException(
                "inbox.scope_missing",
                "Reconnect this account and allow reading mail to reply from your inbox.");
        }

        try
        {
            return (account, await tokenRefresh.GetValidContextAsync(accountId, ct));
        }
        catch (RefreshTokenRevokedException)
        {
            throw new ConflictException("inbox.account_needs_reconnect", "This account needs to be reconnected.");
        }
    }

    /// <summary>Translates provider failures into app errors, flipping the account when access is gone.</summary>
    private async Task<T> CallProviderAsync<T>(
        ConnectedEmailAccount account, Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (ProviderSendException ex)
        {
            switch (ex.Kind)
            {
                case ProviderErrorKind.AuthRevoked or ProviderErrorKind.AuthExpired:
                    account.MarkNeedsReconnect(AccountStateReasons.InvalidGrant);
                    await db.SaveChangesAsync(ct);
                    throw new ConflictException(
                        "inbox.account_needs_reconnect", "This account needs to be reconnected.");

                case ProviderErrorKind.InsufficientScope:
                    account.MarkNeedsReconnect(AccountStateReasons.InsufficientScope);
                    await db.SaveChangesAsync(ct);
                    throw new ConflictException(
                        "inbox.scope_missing",
                        "Reconnect this account and allow reading mail to reply from your inbox.");

                case ProviderErrorKind.PermanentOther:
                    throw new NotFoundException();

                default:
                    throw new ProviderUnavailableException(ex.SafeMessage);
            }
        }
    }
}
