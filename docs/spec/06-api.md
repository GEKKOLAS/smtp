# 06 — REST API Specification

Base: `/api/v1`. JSON everywhere (`application/json; charset=utf-8`).

**Global conventions**

- **Auth:** all endpoints require the session cookie except those marked `anon`. Non-GET
  requires `X-CSRF-Token`. Authorization is always "owner-only" — resources belonging to
  another user return **404**.
- **Errors:** RFC 7807 ProblemDetails + `errorCode`; validation failures → **422** with
  `errors: { field: [messages] }`. Common statuses apply everywhere and are not repeated
  per endpoint: `401` no/expired session · `403` CSRF failure · `404` not found/not owned
  · `422` validation · `429` rate limited.
- **Pagination:** `?page=1&pageSize=20` (max 100) → `{ items, page, pageSize, totalCount }`.
- **Timestamps:** ISO 8601 UTC. **IDs:** UUID strings.
- **Idempotency:** `POST /sends`, `POST /assets/uploads/{id}/complete` accept
  `Idempotency-Key` header (returns the original response on replay).

---

## 1. Auth

| Method & route | Auth | Success |
|---|---|---|
| `POST /auth/register` | anon | 201 |
| `POST /auth/login` | anon | 200 |
| `POST /auth/logout` | ✔ | 204 |
| `POST /auth/logout-all` | ✔ | 204 |
| `POST /auth/password/forgot` | anon | 202 |
| `POST /auth/password/reset` | anon | 204 |
| `GET /auth/sessions` | ✔ | 200 |
| `DELETE /auth/sessions/{id}` | ✔ | 204 |

**POST /auth/register** — body `{ email, password, displayName }`.
Validation: email RFC 5322 + ≤ 320 chars; password ≥ 12 ≤ 128 chars; displayName 1–100.
201 → `{ user: { id, email, displayName } }` + session cookie + CSRF cookie. Duplicate
email → still 201-shaped generic success? **No** — returns 201 with the same body shape
but no session when the email exists (anti-enumeration compromise: message "check your
inbox to verify"); actual duplicate never created. 429 on abuse.

**POST /auth/login** — `{ email, password }` → 200 `{ user }` + cookies. 401
`auth.invalid_credentials` (same for unknown email / wrong password).

**POST /auth/password/forgot** — `{ email }` → always 202 `{}`.
**POST /auth/password/reset** — `{ token, newPassword }` → 204; 400 `auth.invalid_token`
(expired/used/unknown). Revokes all sessions.

## 2. Current user

| Method & route | Success | Notes |
|---|---|---|
| `GET /me` | 200 `{ id, email, displayName, defaultAccountId, createdAt }` | |
| `PATCH /me` | 200 updated profile | body `{ displayName? }`; 1–100 chars |
| `POST /me/password` | 204 | `{ currentPassword, newPassword }`; 400 `auth.wrong_password`; revokes other sessions |

## 3. Provider OAuth

| Method & route | Auth | Success |
|---|---|---|
| `GET /oauth/{provider}/start?returnTo=/accounts` | ✔ | 200 `{ authorizationUrl }` |
| `GET /oauth/{provider}/callback?code&state` | ✔ (session) | 302 → frontend |

`provider ∈ {gmail, outlook}` (route constraint).
**start**: creates single-use state row (PKCE S256). Validation: `returnTo` must be an
app-relative allowlisted path. 409 `oauth.account_limit` if per-user account cap reached (MVP: 5).
**callback**: on any failure (state invalid/expired/foreign, code exchange error, scope
missing) → 302 to `/accounts?error=<code>` with audit entry; **no error details in URL
beyond a code**. On success 302 `/accounts?connected={provider}`. Never returns tokens.

## 4. Connected email accounts

| Method & route | Success | Notes |
|---|---|---|
| `GET /email-accounts` | 200 `{ items: [Account] }` | |
| `GET /email-accounts/{id}` | 200 `Account` | |
| `POST /email-accounts/{id}/default` | 204 | clears previous default atomically |
| `POST /email-accounts/{id}/test` | 200 `{ ok, profile?: { email, displayName }, errorCode? }` | live `GetProfileAsync` call; also surfaces `needs_reconnect` |
| `DELETE /email-accounts/{id}` | 204 | best-effort provider revoke; wipes tokens; cancels queued sends → audit |

`Account = { id, provider, emailAddress, displayName, state, stateReason, isDefault,
grantedScopes, connectedAt, lastUsedAt }` — **never any token material**.
409 `account.has_active_sends` if a job is mid-`sending` (retry disconnect after it settles).

## 5. Templates

| Method & route | Success | Notes |
|---|---|---|
| `GET /templates?search=&archived=false&page=` | 200 paged `TemplateSummary` | sorted `updatedAt DESC` |
| `POST /templates` | 201 `Template` | creates template + version 1 |
| `GET /templates/{id}` | 200 `Template` (incl. `currentVersion`) | |
| `PATCH /templates/{id}` | 200 | `{ name?, description? }` metadata only; 409 `template.name_taken` |
| `POST /templates/{id}/duplicate` | 201 new `Template` | copies latest version |
| `POST /templates/{id}/archive` / `/unarchive` | 204 | |
| `DELETE /templates/{id}` | 204 | soft delete; history intact |

**POST /templates** body:
```json
{ "name": "Welcome", "description": null,
  "content": { "editorKind": "visual|mjml|html", "subject": "…", "preheader": null,
               "mjmlSource": null, "grapesProject": null, "htmlBody": "<p>…</p>",
               "textBody": null, "variablesSchema": [ { "name": "firstName", "type": "text",
                 "required": true, "default": null, "sample": "Ada" } ],
               "assets": [ { "assetId": "…", "usage": "inline_cid|hosted_image|attachment", "contentId": "logo" } ] } }
```
Validation: name 1–200 unique per user; subject 1–500; exactly one source of truth per
`editorKind` (visual ⇒ `grapesProject`+`mjmlSource` required; mjml ⇒ `mjmlSource`; html ⇒
`htmlBody`); variable names `^[a-zA-Z][a-zA-Z0-9_]{0,63}$`, unique; every `assetId` owned
+ `ready`; `contentId` required iff usage `inline_cid`, `^[a-zA-Z0-9._-]{1,100}$`.
Server compiles MJML → html, sanitizes, generates text if absent. MJML compile errors →
422 `template.mjml_invalid` with `{ line, column, message }[]`.

## 6. Template versions

| Method & route | Success | Notes |
|---|---|---|
| `GET /templates/{id}/versions` | 200 paged `{ id, versionNumber, editorKind, createdAt, createdBy }` | |
| `POST /templates/{id}/versions` | 201 `TemplateVersion` | body = `content` object above; becomes current |
| `GET /templates/{id}/versions/{versionId}` | 200 full `TemplateVersion` | |
| `POST /templates/{id}/versions/{versionId}/restore` | 201 new version | copy-as-new, becomes current |

Versions are immutable — no PATCH/DELETE. 409 `template.version_conflict` if
`If-Match`/`baseVersionNumber` provided and stale (editor concurrency guard).

## 7. Assets

Presigned two-phase upload:

| Method & route | Success | Notes |
|---|---|---|
| `POST /assets/uploads` | 201 `{ assetId, uploadUrl, headers, expiresAt }` | body `{ filename, mimeType, sizeBytes, kindHint? }` |
| `POST /assets/uploads/{assetId}/complete` | 200 `Asset` | server verifies object: magic bytes, size, checksum; 422 `asset.verification_failed` (+ object deleted) |
| `GET /assets?kind=&search=&page=` | 200 paged `Asset` | only `ready` |
| `GET /assets/{id}` | 200 `Asset` | |
| `GET /assets/{id}/download-url` | 200 `{ url, expiresAt }` | presigned GET, 5 min |
| `POST /assets/{id}/visibility` | 200 `Asset` | `{ access: "public"|"private" }`; public only for image/gif kinds |
| `DELETE /assets/{id}` | 204 | 409 `asset.in_use` with `{ usages: [{templateId, templateName, versionNumber}] }` unless `?force=true` (then soft-delete; storage cleanup deferred until unreferenced) |

`Asset = { id, kind, originalFilename, mimeType, sizeBytes, access, publicUrl?, width?,
height?, checksumSha256, createdAt }`.
Upload validation: filename 1–255 sanitized; mimeType in allowlist (01-prd/04-security);
sizeBytes ≤ 10 MB (image/gif) or 25 MB (other); per-user storage quota (MVP 1 GB) → 409
`asset.quota_exceeded`.

## 8. Rendering / preview

| Method & route | Success | Notes |
|---|---|---|
| `POST /render/preview` | 200 `{ html, text, subject, preheader, warnings: [{code,message,line?}] }` | rate-limited `render` policy |
| `POST /render/validate` | 200 `{ valid, errors[], warnings[] }` | MJML/HTML lint + email-client constraint checks |

**preview** body: `{ source: { templateVersionId } | { content: <content object> },
variables: { name: value }, mode: "sample|strict" }`.
`sample` fills missing variables from `sample`/`default`; `strict` 422s on missing
required variables (used pre-send). Response HTML is sanitized and safe to iframe.
Warnings include email-constraint findings: HTML > 102 KB (Gmail clipping), missing alt
text, non-inline CSS in `<style>` beyond header support, width > 640 px, JS/embed stripped.

## 9. Sends (jobs, scheduled sends, test send)

| Method & route | Success | Notes |
|---|---|---|
| `POST /sends` | 202 `SendJob` | creates Queued or Scheduled job |
| `POST /sends/test` | 202 `SendJob (isTest)` | to caller's own address only |
| `GET /sends?status=&accountId=&templateId=&from=&to=&page=` | 200 paged `SendJobSummary` | history + scheduled (filter `status=scheduled`) |
| `GET /sends/{id}` | 200 `SendJob` + `recipients[]` + `events[]` | |
| `GET /sends/{id}/snapshot` | 200 `{ html, text, subject }` | from stored snapshot; 404 if not yet rendered |
| `POST /sends/{id}/cancel` | 202 | allowed in `scheduled|queued|retrying`; `sending` cancels remaining recipients; else 409 `send.not_cancellable` |
| `POST /sends/{id}/retry` | 202 | allowed in `failed|partially_failed`; re-queues failed recipients only |
| `PATCH /sends/{id}/schedule` | 200 | `{ scheduledAt }`; only while `scheduled`; ≥ 2 min future, ≤ 1 y |

**POST /sends** body:
```json
{ "connectedEmailAccountId": "…", "templateVersionId": "…",
  "recipients": [ { "email": "a@b.com", "name": "Ada", "contactId": null,
                    "variableOverrides": { "firstName": "Ada" } } ],
  "variables": { "company": "Acme" },
  "attachments": [ { "assetId": "…", "disposition": "attachment", "filenameOverride": null } ],
  "scheduledAt": null }
```
Validation: account owned + `state=active`; version owned + template not deleted;
1–50 recipients, RFC-valid, deduped case-insensitively; all required variables resolvable
per recipient (job values ∪ overrides ∪ defaults) — 422 lists
`{ recipientEmail, missing: [names] }`; attachments owned + `ready`; **estimated total
size ≤ 25 MB** (422 `send.too_large` with breakdown); `scheduledAt` window as above.
Response `SendJob = { id, status, isTest, accountId, templateId, templateVersionNumber,
subjectSnapshot, recipientCounts: { pending, sent, failed, cancelled }, scheduledAt,
createdAt, completedAt, failureCode? }`.

**POST /sends/test** body: `{ connectedEmailAccountId, templateVersionId, variables,
attachments? }` — recipient forced to the user's login email (or connected address via
`toSelf: "login"|"account"`), subject prefixed `[TEST]`.

## 10. Contacts & groups

| Method & route | Success | Notes |
|---|---|---|
| `GET /contacts?search=&groupId=&page=` · `POST /contacts` · `GET/PATCH/DELETE /contacts/{id}` | 200/201/204 | `{ email, firstName?, lastName?, company?, customFields? }`; email unique per user → 409 `contact.duplicate` |
| `POST /contacts/import` | 202 `{ imported, skipped, errors[] }` | CSV ≤ 1 MB, explicit user upload only |
| `GET /contact-groups` · `POST /contact-groups` · `PATCH/DELETE /contact-groups/{id}` | | name unique per user |
| `PUT /contact-groups/{id}/members` | 204 | `{ contactIds: [] }` replace; ≤ 500/group MVP |

## 11. Drafts

| Method & route | Success |
|---|---|
| `GET /drafts` · `POST /drafts` · `GET/PATCH/DELETE /drafts/{id}` | standard CRUD; PATCH is partial (autosave); body mirrors `POST /sends` fields, all optional |

## 12. Audit logs

| Method & route | Success | Notes |
|---|---|---|
| `GET /audit-logs?action=&from=&to=&page=` | 200 paged `{ id, action, entityType, entityId, ip, metadata, createdAt }` | read-only, own events only |

---

## Error code catalog (non-exhaustive)

| errorCode | Status | Meaning |
|---|---|---|
| `auth.invalid_credentials` | 401 | login failed |
| `oauth.state_invalid` | 302→`?error=` | state missing/expired/foreign |
| `oauth.scope_missing` | 302→`?error=` | user denied send scope |
| `account.needs_reconnect` | 409 | action requires reconnect |
| `template.mjml_invalid` | 422 | compile errors with positions |
| `template.name_taken` | 409 | |
| `asset.verification_failed` | 422 | magic-byte/MIME/size mismatch |
| `asset.in_use` / `asset.quota_exceeded` | 409 | |
| `send.too_large` | 422 | > 25 MB budget |
| `send.not_cancellable` / `send.not_retryable` | 409 | wrong state |
| `send.account_needs_reconnect` | 409 | fail-fast pre-queue check |
