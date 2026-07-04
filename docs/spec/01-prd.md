# 01 — Product Requirements Document (PRD)

## 1. Problem statement

People who send recurring, individually addressed email (sales follow-ups, onboarding,
invoices, recruiting outreach, internal comms) rebuild the same messages by hand in Gmail
or Outlook. Design breaks across clients, images get pasted inconsistently, and there is
no history of what was sent, from which account, or whether it failed.

**Mail Template Hub** lets a user connect their real Gmail/Outlook mailboxes via OAuth,
design responsive templates once (visually or in MJML), manage a reusable asset library,
and send or schedule personalized emails from their own address — with full send history,
retries, and audit logging. Mail is sent **through the provider's own API**, so deliverability,
Sent-folder behavior, and account reputation stay with the user's mailbox.

## 2. Target users / personas

| Persona | Needs | Success looks like |
|---------|-------|--------------------|
| **Solo professional** (consultant, recruiter, freelancer) | Send polished, branded one-to-few emails from their own Gmail/Outlook | Template → fill 3 variables → send in under 60 seconds |
| **Small team operator** (ops/CS at a small company) | Consistent transactional-ish messages, several sender accounts | Switch sending account per message; history answers "did we send it?" |
| **Developer/power user** | Control over the HTML, MJML source, variables | Import MJML, test-send, export HTML |

Ownership model for v1 is **single-user tenancy**: every resource belongs to exactly one
user. The schema keeps `UserId` on every owned row so a later move to organizations is a
column addition (`OrganizationId`), not a redesign.

## 3. Core capabilities (full product)

1. **Accounts** — register/login/logout/password reset; never store mailbox passwords.
2. **Provider connections** — OAuth 2.0 authorization-code + PKCE for Gmail and Microsoft;
   encrypted refresh tokens; reconnect/disconnect; default sending account.
3. **Templates** — create/edit/duplicate/archive/delete; immutable versions; subject,
   preheader, MJML source, GrapesJS project JSON, compiled HTML, plain-text fallback;
   `{{variable}}` placeholders; inline (CID) and hosted images; GIFs; attachments;
   dynamic (conditional/repeatable) sections.
4. **Assets** — upload images/GIFs/PDFs/docs with type/size/MIME/content validation;
   media library; stable URLs; unused-asset cleanup.
5. **Sending** — pick account → recipients → template → variables → attachments;
   render → sanitize → MIME → provider send; statuses Queued/Sending/Sent/Failed/
   Retrying/Cancelled; scheduled sends; test send to self; provider message-id capture;
   rendered snapshot stored for audit.
6. **History & audit** — filterable send history; per-recipient status; audit log of
   security-relevant actions.

## 4. MVP scope (must ship first)

| # | Capability | In MVP | Notes |
|---|-----------|:------:|-------|
| 1 | Register / login / logout / password reset | ✅ | Cookie session, argon2id hashes |
| 2 | Connect one Gmail account | ✅ | `gmail.send` + identity scopes only |
| 3 | Connect one Outlook account | ✅ | `Mail.Send`, `User.Read`, `offline_access` |
| 4 | Default sending account | ✅ | |
| 5 | Create / edit / duplicate / delete templates | ✅ | Versioning: auto-version on save |
| 6 | GrapesJS visual editor + MJML source editor | ✅ | `grapesjs-mjml`; two tabs, one source of truth (MJML) |
| 7 | Upload images / GIFs / files | ✅ | 10 MB images, 25 MB files (configurable) |
| 8 | Embed image/GIF (hosted URL or CID) | ✅ | Per-image choice in editor |
| 9 | Preview rendered email (desktop/mobile) | ✅ | Server-rendered preview endpoint |
| 10 | Test send to self | ✅ | |
| 11 | Real send to entered recipients | ✅ | Manual recipient entry; contacts optional |
| 12 | Send history with per-recipient status | ✅ | |
| 13 | Retry transient failures | ✅ | Hangfire retries + manual retry button |
| 14 | Scheduled sends | ✅ | Single future timestamp |
| 15 | Audit log (connect/disconnect/send/template/asset/token-refresh-failure) | ✅ | Write path in MVP; UI read-only list |
| — | Contact groups, drafts UI, provider sync, dynamic sections, React Email engine | ⏳ post-MVP | Schema supports them from day one |

## 5. Non-goals (explicit)

- **No mass-marketing automation**: no drip campaigns, no audience segmentation, no
  open/click tracking pixels in v1.
- **No spam tooling**: hard cap on recipients per send job (default 50, configurable);
  no list rental/import of purchased lists; provider rate limits respected, never bypassed.
- **No inbox reading**: we request send-only scopes. No scraping contacts from the inbox,
  no storing inbox content. (Optional post-MVP "provider sync" reads only Sent-message
  status for messages *we* sent, behind an explicit extra consent.)
- **No password/SMTP auth** for Gmail or Outlook — OAuth only.
- **No multi-user organizations** in v1 (schema is forward-compatible).

## 6. Constraints & assumptions

- Gmail `gmail.send` is a **restricted scope**: production access requires Google's
  OAuth verification and possibly a CASA security assessment. Until verified, the app
  runs with test users (refresh tokens expire after 7 days in testing mode). This is a
  launch-blocking external dependency — start verification early.
- Microsoft `Mail.Send` (delegated) needs admin consent only in tenants that restrict
  user consent; the app must handle `AADSTS65001`-style consent errors gracefully.
- Attachment ceilings differ: Graph `sendMail` JSON call ≈ 4 MB total; larger requires
  draft + upload session (≤ 150 MB per attachment, mailbox limit typically 25–35 MB total).
  Gmail raw send ≈ 25 MB effective. The product enforces a **25 MB total message budget**
  and per-provider strategy is handled in the send layer (see 07-providers.md).
- Provider quotas (Gmail ~250 quota units/user/sec; per-day send caps by account type;
  Graph throttling via 429 + `Retry-After`) shape the retry policy — sends are queued,
  never fired synchronously from the API request thread.

## 7. Success metrics (MVP)

| Metric | Target |
|--------|--------|
| Time from template pick → sent (returning user) | < 60 s median |
| Send success rate excluding invalid recipients | > 99% after retries |
| OAuth connect completion rate | > 90% of started flows |
| Rendering parity: preview vs. Gmail/Outlook actual | No layout-breaking diffs on the 12 canonical test templates |
| Zero incidents of token leakage / cross-user data access | 0 |

## 8. Key risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Google restricted-scope verification delays launch | High | Begin verification in Phase 1; run closed beta with test users |
| Refresh token revocation mid-send | Medium | `NeedsReconnect` account state, user notification, no silent failures |
| Stored XSS via template HTML | High | Server-side allowlist sanitization on every render; CSP on preview iframe (04-security.md) |
| SSRF via remote image URLs | Medium | No server-side fetching of user URLs in MVP; CID embedding only from our own asset store |
| MJML .NET port fidelity gaps vs. reference mjml | Medium | Golden-file tests against reference output; Node sidecar fallback documented |
