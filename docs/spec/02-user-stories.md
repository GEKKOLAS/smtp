# 02 — User Stories with Acceptance Criteria

Format: `US-<area>-<n>`. Priority: **M**VP / **P**ost-MVP. AC are testable Given/When/Then.

## Auth & profile

**US-AUTH-1 (M)** — As a visitor, I can register with email + password so I can use the app.
- Given a valid email and a password ≥ 12 chars, when I register, then an account is created, the password is stored as an argon2id hash, and I am signed in.
- Given an already-registered email, when I register, then I get a generic "check your email" style response that does not disclose account existence, and no duplicate is created.
- Password is never logged or returned by any API.

**US-AUTH-2 (M)** — As a user, I can log in and log out.
- Given valid credentials, when I log in, then I receive an HttpOnly, Secure, SameSite session cookie and a CSRF token.
- Given 10 failed logins for one account/IP within 15 min, further attempts return 429.
- When I log out, the server session is invalidated (not just the cookie deleted).

**US-AUTH-3 (M)** — As a user, I can reset a forgotten password.
- Reset tokens are single-use, expire in 30 min, and are stored hashed.
- Requesting a reset for an unknown email returns the same 202 response as a known email.
- Completing a reset invalidates all existing sessions for that user.

## Provider connections

**US-CONN-1 (M)** — As a user, I can connect my Gmail account.
- When I click "Connect Gmail", I am redirected to Google consent requesting only `openid email profile https://www.googleapis.com/auth/gmail.send`.
- On callback, the state parameter is validated (single-use, ≤10 min old, bound to my session) and PKCE verifier is sent; otherwise the connection is rejected and audited.
- On success, the account appears in my connected list with email, display name, provider, and scope set; tokens are stored encrypted and never appear in any API response.
- Connecting the same Google account twice updates the existing row (upsert on provider + provider_account_id), it does not duplicate.

**US-CONN-2 (M)** — As a user, I can connect my Outlook / Microsoft 365 account. (Same AC as US-CONN-1 with scopes `openid profile email offline_access User.Read Mail.Send` and tenant metadata captured.)

**US-CONN-3 (M)** — As a user, I can disconnect an account.
- When I disconnect, the app revokes the token at the provider (best-effort), deletes stored tokens, marks the account `Revoked`, and writes an audit entry.
- In-flight send jobs using that account fail with a clear "account disconnected" error; queued jobs are cancelled.

**US-CONN-4 (M)** — As a user, I can set a default sending account.
- Exactly one account per user may be default (DB-enforced partial unique index).
- Compose screens pre-select the default account.

**US-CONN-5 (M)** — As a user, I am told when a connection is broken.
- Given my refresh token was revoked or expired, when a refresh fails with `invalid_grant`, then the account state becomes `NeedsReconnect`, a "Reconnect" CTA appears, and an audit event `token.refresh_failed` is written. Sends via that account fail fast with a typed error, not a timeout.

## Templates

**US-TPL-1 (M)** — As a user, I can create a template with subject, preheader, and body.
- Creating a template creates version 1; the template list shows name, updated time, and thumbnail-safe description.
- Names are required, ≤ 200 chars, unique per user among non-deleted templates.

**US-TPL-2 (M)** — As a user, I can edit visually (GrapesJS) or in MJML source, interchangeably.
- Saving from the visual editor persists GrapesJS project JSON **and** exported MJML **and** compiled HTML in the same version.
- Switching to source view shows the current MJML; editing MJML and saving recompiles HTML and invalidates stale GrapesJS JSON with a visible "visual layout was rebuilt from source" notice.
- Invalid MJML blocks save with row/column error positions.

**US-TPL-3 (M)** — As a user, saving creates a version I can return to.
- Each save creates an immutable `EmailTemplateVersion` with an incrementing number.
- I can list versions, preview any version, and restore one (restore = new version copying the old content).

**US-TPL-4 (M)** — As a user, I can duplicate, archive, and delete templates.
- Duplicate copies the latest version into a new template named "Copy of …".
- Archived templates are hidden from compose but visible under an "Archived" filter.
- Delete is soft (`DeletedAt`); historical send records still resolve the version they used.

**US-TPL-5 (M)** — As a user, I can use `{{variables}}`.
- The editor lists variables detected in subject/preheader/body; unknown variables at send time block the send with a per-variable validation error (unless marked optional with a default).
- `{{unsubscribeUrl}}` and other URL-type variables are validated as absolute http(s) URLs at render time.

**US-TPL-6 (M)** — As a user, I can put images and GIFs in a template.
- I can pick from the asset library or upload inline; per-image I choose **Hosted** (public URL) or **Inline (CID)**.
- CID-marked assets are recorded on the template version so the send pipeline attaches them.

**US-TPL-7 (M)** — As a user, I can preview and test-send.
- Preview shows rendered HTML (variables filled with sample values) in desktop (600px+) and mobile (375px) frames, plus the plain-text version.
- Test send delivers to my own login email (or the connected account's address) with subject prefixed `[TEST]`, counted in history as `IsTest = true`.

**US-TPL-8 (P)** — Import HTML/MJML; export HTML. Export always available; import parses MJML directly and wraps raw HTML in a `mj-raw` fallback with a fidelity warning.

## Assets

**US-AST-1 (M)** — As a user, I can upload images, GIFs, PDFs, and documents.
- Allowed types (MVP): png, jpg/jpeg, gif, webp, pdf, docx, xlsx, pptx, txt, csv, zip. Everything else is rejected with 422.
- Server verifies magic bytes match the claimed MIME; mismatch ⇒ reject + audit.
- Limits: images/GIFs ≤ 10 MB, other files ≤ 25 MB.
- Stored with SHA-256 checksum; an identical checksum for the same user dedupes to the existing asset.

**US-AST-2 (M)** — As a user, I can browse and manage my asset library (grid, filter by type, search by filename, delete). Deleting an asset used by a template version shows the usage list and requires confirmation; the object is soft-deleted and physically removed later by the cleanup job only when unreferenced.

**US-AST-3 (M)** — Asset access is private by default. Public URLs exist only for assets explicitly marked hosted-public (needed for hosted images in delivered email); everything else is served via short-lived presigned URLs scoped to the owner.

## Sending

**US-SND-1 (M)** — As a user, I can send a templated email from a connected account.
- Flow: choose account → template (latest or pinned version) → recipients (≤ 50) → fill variables → optional attachments → review → send.
- Submitting creates an `EmailSendJob` (status `Queued`) and returns 202 immediately; the UI shows live status.
- Total message budget (body + inline images + attachments) ≤ 25 MB, validated before queueing.

**US-SND-2 (M)** — As a user, I can schedule a send.
- I pick a future UTC-normalized time (≥ 2 min ahead, ≤ 1 year); the job stores `ScheduledAt` and status `Scheduled`.
- I can cancel or reschedule any time before promotion to `Queued`.

**US-SND-3 (M)** — As a user, I can see send history.
- Filter by status, account, template, date range; each job shows per-recipient status and provider message id when available.
- I can open a stored rendered snapshot of what was sent.

**US-SND-4 (M)** — Failed sends retry sensibly.
- Transient errors (429, 5xx, network) auto-retry with exponential backoff + jitter (5 attempts, see 10-jobs.md); status shows `Retrying` with next-attempt time.
- Permanent errors (invalid recipient, revoked token, message too large) fail immediately with a typed reason and no auto-retry; a manual "Retry" action re-queues after the user fixes the cause.

**US-SND-5 (M)** — As a user, I can cancel a queued/scheduled job. Cancelling a job in `Sending` affects only recipients not yet attempted.

## Contacts (post-MVP UI, MVP schema)

**US-CNT-1 (P)** — Manage contacts (email, first/last name, company, custom fields) and groups; compose can target a group (still subject to per-send recipient cap).

## Audit & settings

**US-AUD-1 (M)** — As a user, I can view an audit trail of security-relevant events (connect, disconnect, send, template change, asset upload, token refresh failure, logins) with timestamp, action, IP, and summary — read-only, newest first.

**US-SET-1 (M)** — As a user, I can manage profile (display name), change password (requires current password, invalidates other sessions), and see/revoke active sessions.
