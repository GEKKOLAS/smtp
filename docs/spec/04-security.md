# 04 — Security Model

## 1. App authentication (users ↔ our API)

**Decision: server-side sessions in HttpOnly cookies, not JWTs in JS.**
The Next.js server proxies `/api/*` to the backend on the same public origin, so cookies
work with `SameSite=Lax` and no token is ever readable by frontend JavaScript.

| Item | Spec |
|------|------|
| Password hashing | Argon2id (Isopoh/`Konscious`), params: 64 MB memory, 3 iterations, parallelism 2; per-user salt; rehash-on-login when params change |
| Session | Random 256-bit id, stored server-side (`UserSessions` table): hash of id, user, created, last-seen, IP, UA, absolute expiry 30 d, idle expiry 7 d |
| Cookie | `__Host-mth_session`: HttpOnly, Secure, SameSite=Lax, Path=/ |
| CSRF | Double-submit: `mth_csrf` readable cookie + `X-CSRF-Token` header required on all non-GET; validated by middleware; OAuth callback protected separately via `state` (below) |
| Login throttling | ASP.NET rate limiter: 10/15 min per (account), 30/15 min per IP on `/auth/login`, `/auth/password/*`; generic 429 |
| Password reset | Token = 256-bit random, stored SHA-256, TTL 30 min, single use; completing reset revokes all sessions |
| Enumeration | Register/forgot-password return identical 202 shapes regardless of account existence |
| Logout | Deletes session row; "log out everywhere" deletes all rows for user |

## 2. Provider OAuth (users ↔ Gmail/Microsoft)

### Flow (authorization code + PKCE, confidential client)

```mermaid
sequenceDiagram
    participant B as Browser
    participant A as API
    participant P as Provider (Google/MS)

    B->>A: GET /oauth/{provider}/start (session cookie)
    A->>A: create state row {id, userId, provider, pkceVerifier, returnTo, exp=10min}
    A-->>B: 200 { authorizationUrl } (state=id, code_challenge=S256)
    B->>P: consent screen
    P->>A: GET /oauth/{provider}/callback?code&state
    A->>A: load+delete state row (single use), check exp, check state.userId == session.userId
    A->>P: token exchange (code + verifier + client_secret) — server to server
    A->>P: fetch profile (id_token claims / Graph /me)
    A->>A: upsert ConnectedEmailAccount + encrypted OAuthToken; audit account.connected
    A-->>B: 302 → /accounts?connected={provider} (no tokens in URL, ever)
```

Hardening rules:

- `state` is an opaque random id referencing a **server-side row** (holds PKCE verifier
  too) — single-use, 10-minute TTL, bound to the initiating session's user. Mismatch ⇒
  403 + `audit oauth.state_rejected`.
- PKCE S256 always, for both providers, even though we are a confidential client.
- Exact-match registered redirect URIs; no wildcard, no open `returnTo` (allowlist of
  app-relative paths only).
- Google: `access_type=offline`, `prompt=consent` (guarantees refresh token),
  `include_granted_scopes=false`. Verify `id_token` audience + issuer; use `sub` as
  `ProviderAccountId`.
- Microsoft: `/common` authority (multi-tenant + personal); capture `tid` and `oid`;
  `ProviderAccountId = oid` (or `sub` for MSA). Request `offline_access` explicitly.
- Scope check on callback: granted scopes (Google returns `scope` field; MS in token
  response) must include the send scope; if the user unchecked it, mark the account
  `NeedsReconnect(reason=insufficient_scope)` and tell them why.
- Scopes requested (least privilege):
  - Google: `openid email profile https://www.googleapis.com/auth/gmail.send`
  - Microsoft: `openid profile email offline_access User.Read Mail.Send`

### Token storage & encryption

- Table `OAuthTokens` (1:1 with account, see 05-database.md). Columns store **ciphertext
  only** for access + refresh tokens.
- `ITokenCipher` = AES-256-GCM envelope encryption:
  - Data-encryption key (DEK) generated per token row; DEK wrapped by a key-encryption
    key (KEK) from configuration (dev: env var; prod: KMS/HSM-provided key).
  - Row stores: `ciphertext`, `nonce`, `wrappedDek`, `kekVersion`. KEK rotation =
    rewrap DEKs in a background sweep, no provider re-consent needed.
  - AAD = `connectedEmailAccountId` — a ciphertext copied to another row fails to decrypt.
- Tokens are decrypted only inside `ITokenRefreshService` in the worker/API process,
  held in memory for the duration of a call, never logged (Serilog destructuring policy
  redacts `Authorization`, `access_token`, `refresh_token` keys), never serialized into
  any DTO. **No API response contains a token field, even encrypted.**

### Refresh strategy

- On use: if `ExpiresAt - now < 5 min` ⇒ refresh first. Per-account distributed lock
  (Postgres advisory lock on account id) prevents concurrent refresh races; both providers
  may rotate refresh tokens (Microsoft always does), so the losing thread re-reads the row.
- Proactive: hourly Hangfire sweep refreshes tokens expiring < 30 min with pending work
  (scheduled jobs) so scheduled sends don't pay refresh latency.
- `invalid_grant` / `interaction_required` ⇒ account `NeedsReconnect`, tokens wiped,
  audit `token.refresh_failed`, queued jobs for the account transition to `Failed(auth_revoked)`.

## 3. Template & content security

| Threat | Control |
|--------|---------|
| **Stored XSS** (template HTML executes in our preview or another page) | (1) Server-side sanitization with Ganss.Xss allowlist on **every** render (preview and send) — strips `<script>`, event handlers, `javascript:`/`data:text/html` URIs, `<iframe>`, `<object>`, forms; allows email-safe tags/attrs incl. `style`, `table`, `img`, VML conditional comments for Outlook buttons. (2) Preview is delivered as `text/html` from a **sandboxed iframe** (`sandbox="allow-same-origin"` minus scripts) with `Content-Security-Policy: default-src 'none'; img-src https: data:; style-src 'unsafe-inline'`. (3) API responses embedding HTML are JSON-encoded strings, never reflected. |
| **SSRF via remote images** | The server never fetches user-supplied URLs in MVP. CID inlining reads only from our object store by asset id (ownership-checked). Post-MVP "import image from URL" must go through a fetcher that resolves DNS, rejects private/link-local/metadata ranges (169.254.0.0/16, 10/8, 172.16/12, 192.168/16, ::1, fd00::/8), caps size/time, and follows ≤ 3 redirects re-validating each hop. |
| **HTML injection via variables** | Variable values are HTML-encoded by default at render time; a variable must be explicitly typed `html` in the template schema to be inserted raw, and raw insertions pass through the sanitizer afterwards anyway. URL-typed variables must parse as absolute `http(s)` URLs. |
| **Header injection** | Subject/recipient values are CR/LF-stripped; MimeKit encodes headers, we additionally reject any control chars in address inputs. |

## 4. Upload security

- Presigned PUT restricted to: exact key (`assets/{userId}/{assetId}/{safeName}`), exact
  `Content-Type`, `content-length-range` enforced, 10-minute expiry.
- On `complete`: server reads the object head + first bytes, verifies **magic bytes vs.
  declared MIME** (png/jpg/gif/webp/pdf/zip signatures; docx/xlsx are zip + content-type
  check), verifies size, computes SHA-256, rejects extension/MIME mismatch (422 + audit).
- Denylist regardless of claim: executables, scripts, HTML/SVG as *hosted image* (SVG can
  carry scripts; SVGs are allowed only as downloadable attachments, never as hosted/public
  images), files > limits.
- Public bucket policy applies only to keys under `public/`; assets become public **only**
  when the user marks an image "hosted" (copy/move to public prefix). Everything else is
  private, served via presigned GET (5 min) after an ownership check.
- Bucket blocks: no bucket-wide public listing; separate buckets (or prefixes) for
  `private/`, `public/`, `snapshots/`.

## 5. Authorization (object ownership)

- Every owned entity carries `UserId`. Every query in Application handlers filters by
  `CurrentUser.Id` — enforced by convention **and** by a global EF query filter
  (`entity.UserId == _currentUser.Id`) on owned entities; background jobs run with an
  explicit `IgnoreQueryFilters` + job-carried user id.
- Cross-entity checks on writes: creating a send validates account, template version,
  contacts, and every attachment asset belong to the caller; failure ⇒ 404 (not 403) to
  avoid resource-id oracle.
- IDs are UUIDv7 — unguessable and index-friendly; still never rely on unguessability.

## 6. Rate limiting (ASP.NET Core RateLimiter)

| Policy | Applies to | Limit |
|--------|-----------|-------|
| `auth` | login, register, forgot/reset | 10 / 15 min / user+IP |
| `oauth` | /oauth/*/start, callback | 10 / 10 min / user |
| `send` | POST /sends, /sends/test | 30 / hour / user (MVP guardrail, configurable) |
| `upload` | POST /assets/uploads | 60 / hour / user |
| `render` | POST /render/preview | 120 / min / user (editor autopreview) |
| global | everything | 600 / min / user, 1200 / min / IP |

429 responses include `Retry-After`. Limits live in config, not code.

## 7. Audit log

Append-only `AuditLogs` (05-database.md). Written in the same transaction as the action
where possible; token-refresh failures written by the worker.

| Action code | Trigger |
|-------------|---------|
| `auth.login` / `auth.login_failed` / `auth.logout` / `auth.password_reset` | Auth events |
| `account.connected` / `account.disconnected` / `account.default_changed` | Provider lifecycle |
| `oauth.state_rejected` | Callback CSRF/state failure |
| `token.refreshed` (debounced daily) / `token.refresh_failed` | Token lifecycle |
| `template.created/updated/version_saved/duplicated/archived/deleted/restored` | Template changes |
| `asset.uploaded` / `asset.deleted` / `asset.rejected` | Asset lifecycle |
| `send.created` / `send.scheduled` / `send.cancelled` / `send.retried` / `send.completed` / `send.failed` | Send lifecycle |

Fields: `UserId`, `Action`, `EntityType`, `EntityId`, `Ip`, `UserAgent`, `Metadata jsonb`
(no secrets, no full recipient lists — counts only), `CreatedAt`. No UPDATE/DELETE grants
on this table for the app role.

## 8. Error handling & disclosure

- Global middleware returns RFC 7807 ProblemDetails: `type`, `title`, `status`,
  `traceId`, and machine-readable `errorCode` (e.g. `send.account_needs_reconnect`).
- Never surfaced: stack traces, connection strings, provider raw error bodies (mapped to
  our `ProviderErrorKind` + a safe human message), tokens, internal ids of other users.
- Provider error details are logged server-side at Warning with token scrubbing, and the
  sanitized form is stored on `EmailProviderEvents` for the user's own history.

## 9. Transport & headers

TLS everywhere (HSTS `max-age=31536000; includeSubDomains`). API sets:
`X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
`X-Frame-Options: DENY` (except the sandboxed preview route, which instead sets the CSP
above and `frame-ancestors 'self'`). Next.js sets an app CSP with nonce'd scripts.
Secrets via environment/secret manager; nothing secret in `appsettings.json` committed files.
