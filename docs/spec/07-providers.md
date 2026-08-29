# 07 — Provider Integration Specification (Gmail API + Microsoft Graph)

Covers Deliverables G and 14 (MIME & attachments). All code lives in
`Infrastructure/Providers/{Google,Microsoft}` behind `IEmailProviderClient` (03-architecture.md §3).

## 1. OAuth app registrations

| | Google | Microsoft |
|---|---|---|
| Console | Google Cloud Console → OAuth consent + credentials (Web application) | Entra ID app registration, `AzureADandPersonalMicrosoftAccount` audience |
| Authority | `https://accounts.google.com/o/oauth2/v2/auth` / token `https://oauth2.googleapis.com/token` | `https://login.microsoftonline.com/common/oauth2/v2.0/{authorize,token}` |
| Redirect URI | `https://{api-host}/api/v1/oauth/gmail/callback` | `https://{api-host}/api/v1/oauth/outlook/callback` |
| Scopes | `openid email profile https://www.googleapis.com/auth/gmail.send` + optional `.../gmail.readonly` | `openid profile email offline_access User.Read Mail.Send` + optional `Mail.Read` |
| Refresh token | Only with `access_type=offline&prompt=consent`; long-lived; revocable; expires if unused ~6 months; **7-day expiry while app is in "Testing"** | Always with `offline_access`; **rotates on every refresh** (must persist the new one); ~90-day inactivity window |
| Revocation endpoint | `POST https://oauth2.googleapis.com/revoke?token=` | No universal endpoint for a single refresh token — disconnect = delete our tokens (document to user that consent remains in their MS account page) |
| ⚠️ Provider risk | `gmail.send` is **restricted**: unverified apps capped at 100 test users; production needs OAuth verification (+ possible CASA assessment, weeks of lead time). `gmail.readonly` is **also restricted** and widens that same review | Org tenants may require **admin consent** for `Mail.Send`; handle `consent_required`/`AADSTS65001` by showing an "ask your admin" screen. `Mail.Read` is delegated, no extra admin tier |

**Read scope is optional, never required.** `RequiredScopes` stays send-only, so an
account that declines (or predates) read access connects normally and only loses the
inbox feature. `AccountDto.CanReadInbox` drives the UI's reconnect prompt.

## 2. Token refresh (`ITokenRefreshService`)

```
GetValidContextAsync(accountId):
  load account + token row
  if account.state != active → throw AccountNeedsReconnect
  if access_token expires > 5 min → decrypt, return
  else:
    acquire pg advisory lock (accountId)          // cross-instance safe
    re-read row (another worker may have refreshed)
    still stale → POST token endpoint (refresh_token grant)
      success → encrypt+store new access (and refresh if rotated: MS always,
                Google occasionally), update expiries, release lock
      invalid_grant | interaction_required →
                wipe token row, account.state = needs_reconnect(reason),
                audit token.refresh_failed, EmailProviderEvent(token_refresh_failed),
                throw TokenRefreshException(AuthRevoked)
      5xx/timeout → throw Transient (job retry handles it)
```

- Refresh HTTP calls use a dedicated named `HttpClient` with Polly: 3 tries, decorrelated
  jitter (250 ms base), only on 5xx/network — **never** retry `invalid_grant`.
- Google may *omit* the refresh token on re-consent if one already exists — keep the old
  one unless a new one arrives.

## 3. Sending

### 3.1 Gmail (`GmailEmailProviderClient`)

- Library: `Google.Apis.Gmail.v1`. Endpoint: `users.messages.send` on `userId=me`.
- **Strategy: always raw RFC 2822.** MimeKit builds the full MIME (see §5); we
  base64url-encode and send. For messages > ~5 MB use the multipart/resumable **media
  upload** variant (`/upload/gmail/v1/users/me/messages/send`, `uploadType=resumable`)
  — the SDK's `SendRequest` with a stream handles this.
- Response: `{ id, threadId, labelIds }` → store `provider_message_id = id`,
  `provider_thread_id = threadId`. Gmail auto-saves to Sent.
- From header: must be the connected address (or a verified alias); we always set
  `From = account.EmailAddress` — Gmail rewrites mismatches silently, so never trust
  user-supplied From.

**Gmail error map (`GoogleErrorMap`)**

| Provider signal | `ProviderErrorKind` |
|---|---|
| 401 `authError` / invalid credentials | `AuthExpired` (refresh once, retry) |
| 403 `insufficientPermissions` | `InsufficientScope` |
| `invalid_grant` on refresh | `AuthRevoked` |
| 429 / 403 `rateLimitExceeded`, `userRateLimitExceeded` | `Transient` (honor `Retry-After`, else backoff) |
| 403 `dailyLimitExceeded` / sending quota | `QuotaExceeded` (park ≥ 1 h) |
| 400 `invalidArgument` on recipient | `RecipientRejected` |
| 413 / `payloadTooLarge` | `MessageTooLarge` |
| 5xx / socket / timeout | `Transient` |

Quotas: ~250 quota units/user/sec (`messages.send` = 100 units ⇒ ~2.5 sends/sec/user);
daily send caps ~500 (consumer) / 2000 (Workspace). Worker throttles to
**1 send/sec/account** and treats quota errors as parking signals.

### 3.2 Outlook (`OutlookEmailProviderClient`)

- Library: `Microsoft.Graph` v5. Auth via our own token (a `DelegatedTokenCredential`
  wrapping `ITokenRefreshService` — we do **not** let MSAL own the cache; tokens live in
  our encrypted store).
- **Strategy by size** (Graph request cap ≈ 4 MB):
  - Total MIME ≤ 3 MB → `POST /me/sendMail` with Graph JSON message
    (`saveToSentItems=true`), attachments as `fileAttachment` (base64,
    `isInline + contentId` for CID).
  - Larger → `POST /me/messages` (draft) → per attachment > 3 MB
    `createUploadSession` + chunked PUT (3.2 MB chunks) → `POST /me/messages/{id}/send`.
- `sendMail` returns **202 with no message id**. To capture ids we set a custom
  `internetMessageHeader` `X-MailTemplateHub-Ref: {recipientRowId}`; optional post-MVP
  sync job queries Sent Items by that header. The draft+send path *does* give us the
  message id up front — used automatically for large sends.

**Graph error map (`GraphErrorMap`)**

| Provider signal | Kind |
|---|---|
| 401 `InvalidAuthenticationToken` | `AuthExpired` |
| 403 `ErrorAccessDenied` (scope) | `InsufficientScope` |
| `invalid_grant` / `interaction_required` on refresh | `AuthRevoked` |
| 429 (`Retry-After` header) / 503 `MailboxConcurrency` | `Transient` (honor header — Graph bans clients that ignore it) |
| `ErrorMessageSizeExceeded` / 413 | `MessageTooLarge` |
| `ErrorInvalidRecipients` | `RecipientRejected` |
| `ErrorSendAsDenied` | `PermanentOther` (surface "cannot send as this address") |
| `ErrorMessageSubmissionBlocked` / `ErrorQuotaExceeded` | `QuotaExceeded` |
| 5xx | `Transient` |

Throttle: Graph mail ~10,000 requests/10 min/app/mailbox but *submission* limits are far
lower (typically 30 messages/min, 10k recipients/day for M365). Worker throttles to
**1 send/2 sec/account** default.

## 4. Send orchestration (per recipient)

```mermaid
sequenceDiagram
    participant J as SendEmailJob (Hangfire)
    participant R as ITemplateRenderer
    participant T as ITokenRefreshService
    participant B as IEmailMessageBuilder
    participant P as IEmailProviderClient

    J->>J: claim recipient row (FOR UPDATE SKIP LOCKED, status=pending→sending)
    J->>R: render(version, jobVars ∪ recipientOverrides)  [cached per job for shared parts]
    J->>T: GetValidContextAsync(accountId)
    J->>B: Build(OutgoingEmail)  → MimeMessage + size
    J->>P: SendAsync(ctx, email)
    alt success
        J->>J: recipient=sent, providerMessageId; EmailProviderEvent(send_success)
    else ProviderSendException(Transient/QuotaExceeded)
        J->>J: recipient=pending, attempt++, schedule retry (backoff/Retry-After)
    else AuthExpired
        J->>T: force refresh, retry once, else escalate
    else permanent kinds
        J->>J: recipient=failed(code); AuthRevoked additionally fails whole job + flags account
    end
    J->>J: when no pending recipients → finalize job status (sent / partially_failed / failed)
```

## 5. MIME construction (Deliverable 14) — `MimeKitEmailMessageBuilder`

Structure (MimeKit `BodyBuilder` semantics, built explicitly for control):

```
multipart/mixed                          ← only if file attachments exist
├── multipart/related                    ← only if CID images exist
│   ├── multipart/alternative
│   │   ├── text/plain; charset=utf-8            (always first)
│   │   └── text/html;  charset=utf-8
│   └── image/png|gif|jpeg… (Content-ID: <logo@mth>, Content-Disposition: inline)
└── application/pdf … (Content-Disposition: attachment; filename*=UTF-8)
```

Rules:

- **Always** both `text/plain` and `text/html` alternatives.
- **CID images:** `Content-ID` = `<{contentId}@mailtemplatehub>`; HTML references
  `src="cid:{contentId}@mailtemplatehub"`. GIFs are just `image/gif` inline parts —
  animation support is a client concern (Outlook desktop shows first frame; require a
  meaningful first frame — surfaced as an editor warning).
- **Hosted images:** plain `https://` URLs to our public asset prefix/CDN; no MIME part.
  Trade-off surfaced in the editor: hosted = smaller message but blocked-by-default in
  some clients; CID = always visible but bigger message + spam-score sensitive.
- **Attachments:** streamed from object storage (no full-buffer for large files);
  `filename*` RFC 2231 encoding for non-ASCII names; MIME type from our verified value.
- **Headers:** `Message-ID` generated by MimeKit; `Date` UTC now; custom
  `X-MailTemplateHub-Ref` (recipient row id) for correlation; no `Bcc` in MVP;
  header values sanitized (no CR/LF — enforced upstream too).
- **Size accounting:** builder returns serialized size (base64 inflation ≈ ×1.37 included).
  Enforced budget: **25 MB total**; per-provider hard checks: Gmail reject at > 25 MB;
  Graph switches to upload-session path at > 3 MB and rejects > 25 MB (mailbox-limit safety).
- **Fallback when provider rejects:**
  - `MessageTooLarge` → recipient fails with actionable message ("reduce attachments by X MB");
    no auto-mutation of the user's message.
  - CID rejected / malformed content → error surfaces as failed recipient; user can switch
    images to hosted and retry (one-click "convert inline images to hosted" on retry dialog — post-MVP).

## 5b. Reading the inbox & replying in-thread (`IInboxProviderClient`)

A template can answer a real conversation instead of starting a new one. Reads go
straight to the provider on every call — no message is ever stored.

| | Gmail (`GmailInboxClient`) | Outlook (`OutlookInboxClient`) |
|---|---|---|
| List | `users.messages.list?labelIds=INBOX` returns ids only → one `format=metadata` get per id (bounded concurrency) | `/me/mailFolders/inbox/messages?$select=…` returns full rows in one call |
| Paging | `nextPageToken` | the whole `@odata.nextLink` URL is the page token |
| Search | `q=` | `$search="…"` (mutually exclusive with `$orderby`) |
| Body | `format=full`, depth-first walk of `payload.parts` for `text/html` / `text/plain`, base64url-decoded | `body.content` + `body.contentType` |
| Thread ids | `threadId`, `Message-ID` / `References` headers | `conversationId`, `internetMessageId`, `internetMessageHeaders` |

**Threading a reply.** `POST /sends` takes only `replyToMessageId`; the server re-reads
that message so a client can never inject `In-Reply-To`/`References` values. What it
resolves is frozen onto the job (`reply_to_*` columns) so the background send needs no
second mailbox read:

- `In-Reply-To` = parent's `Message-ID`; `References` = parent's chain + its `Message-ID`.
  Both providers thread on these.
- Gmail additionally needs `threadId` in the send payload — headers alone are not enough,
  which is why `OutgoingEmail.ProviderThreadId` exists.
- Recipients default to the parent's sender; the subject becomes `Re: <parent subject>`
  (not the template's own subject) unless overridden.

Sender-controlled HTML is sanitized server-side and framed in a fully locked
(`sandbox=""`) iframe in the reader.

## 6. Account lifecycle & connection test

- `GetProfileAsync`: Gmail → `users.getProfile(me)` (email); Graph → `/me`
  (`mail ?? userPrincipalName`, `displayName`). Used by `POST /email-accounts/{id}/test`
  and after connect to verify send capability cheaply.
- Disconnect: Google → revoke endpoint (best-effort, 5 s timeout); Microsoft → local
  delete only (see table); both → wipe `oauth_tokens` row, `state=revoked`,
  cancel queued/scheduled jobs on that account, audit.

## 7. Resilience defaults (Polly, per provider client)

| Policy | Setting |
|---|---|
| Timeout | 30 s per attempt (100 s for upload-session chunks) |
| Retry (inside a job attempt) | none — retries are job-level (10-jobs.md) so backoff state survives process restarts |
| Circuit breaker | per provider host: open after 8 consecutive transient failures / 30 s, half-open probe; while open, jobs reschedule +60 s instead of burning attempts |
| Honor `Retry-After` | always wins over computed backoff |
