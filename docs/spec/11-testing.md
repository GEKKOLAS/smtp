# 11 — Testing Strategy

## Test pyramid & tooling

| Layer | Tooling | Scope | Speed budget |
|---|---|---|---|
| Unit (backend) | xUnit + NSubstitute + FluentAssertions + Bogus | Handlers, validators, renderer steps, error maps, token cipher, MIME builder | < 60 s total |
| Integration (backend) | xUnit + **Testcontainers** (PostgreSQL, MinIO) + **WireMock.Net** (Google/MS endpoints) + `WebApplicationFactory` | Full HTTP → DB → storage paths, OAuth callback, jobs executed inline | < 8 min |
| Architecture | NetArchTest | Layer dependency rules (03 §2) | seconds |
| Unit (frontend) | Vitest + React Testing Library + MSW | Components, hooks, zod schemas, api client error mapping | < 60 s |
| E2E | Playwright (against docker-compose stack + WireMock provider doubles) | The 8 critical journeys below | < 15 min |

CI gates (every PR): backend unit+arch, frontend unit, integration; E2E on main + nightly.
Coverage gate: 80% line on `Application` + `Infrastructure/Rendering` + provider error maps
(quality of assertions > raw %, enforced in review).

## Backend unit test focus

- **Validators:** every FluentValidation rule has a pass + fail case (table-driven).
- **Template rendering (08):**
  - Golden files: 12 canonical templates — MJML in → expected HTML out (byte-stable);
    regenerated intentionally via `dotnet run --project tools/regen-goldens`.
  - Sanitizer: XSS corpus (script tags, `onerror`, `javascript:` href, nested/encoded
    payloads, MSO comment preservation, `data:text/html` iframe) — must strip; email-safe
    corpus must survive unchanged.
  - Variables: encoding by type (text encoded, url validated, html sanitized), strict vs
    sample mode, per-recipient override precedence, `{{#if}}/{{#each}}` sections.
  - Plain-text generator: headings, links, tables, images-alt snapshots.
- **MIME builder:** structure assertions by parsing output with MimeKit — alternative
  order (text before html), related/mixed nesting for the 4 combinations (±CID, ±attachments),
  CID header format, RFC 2231 filenames, size accounting vs actual serialized size.
- **Provider error maps:** every mapped provider signal (07 §3 tables) → expected
  `ProviderErrorKind` (table-driven from recorded fixtures).
- **Token cipher:** roundtrip, AAD tamper detection (swap row id ⇒ decrypt fails),
  KEK-version rotation rewrap.
- **Send state machine:** transitions incl. finalization matrix (all sent / partial /
  none), park-on-quota, reaper reset, cancel-during-sending.

## Backend integration tests (Testcontainers)

| Suite | Asserts |
|---|---|
| Auth | register→login→me→logout; cookie flags; CSRF rejection; rate limit 429; reset flow invalidates sessions |
| **OAuth callback** | WireMock plays Google/MS token+profile endpoints: happy path persists encrypted tokens (assert ciphertext ≠ plaintext in DB, decryptable via cipher); state expired/reused/foreign-user ⇒ 302 error + audit row; scope-missing ⇒ `needs_reconnect`; MS refresh-token rotation persisted |
| Ownership | Every resource route × foreign-user id ⇒ 404 (parameterized sweep over all GET/PATCH/DELETE endpoints) |
| Assets | presign→PUT to MinIO→complete: happy; MIME/magic mismatch (gif bytes as .pdf) ⇒ 422 + object deleted; dedupe by checksum; quota; in-use delete 409 with usages |
| Templates/versions | create compiles MJML; invalid MJML 422 with positions; version immutability; restore-as-new; duplicate |
| Sends | POST validates budget/variables per recipient; job runs inline (Hangfire in-memory or direct `SendEmailJob.RunAsync`) against WireMock Gmail/Graph: success persists provider ids + events + snapshot in MinIO; 429 with Retry-After schedules retry; invalid_grant flips account and fails job; partial failure finalization; cancel mid-send |
| Idempotency | duplicate `POST /sends` with same key returns original job |

## Frontend tests

- API client: ProblemDetails → typed errors, 401 redirect, CSRF header injection (MSW).
- Compose wizard: step validation, size-budget bar math, strict-preview 422 routes user
  to the right step with messages.
- Editor shell: dirty tracking, save-conflict dialog paths, variable panel detection
  rendering (GrapesJS itself mocked at the boundary — its internals are not ours to test).
- FilePond flow: presign→PUT→complete happy + rejection rendering.

## E2E journeys (Playwright, provider doubles via WireMock)

1. **Connect Gmail** — click connect → (double consent page) → callback → account card
   shows active + audited.
2. **Connect Outlook** — same incl. simulated admin-consent error path.
3. **Create template** — visual editor: drag text+button+image blocks, save version,
   version badge increments.
4. **Upload GIF** — dropzone → library shows GIF badge, dims.
5. **Embed image** — from editor asset picker as hosted **and** as CID; preview shows both.
6. **Send test email** — editor test-send dialog → WireMock asserts raw MIME contains
   `[TEST]` subject + CID part.
7. **Schedule email** — compose → schedule +5 min (test clock) → appears in scheduled →
   promote tick → history shows sent.
8. **Retry failed email** — WireMock scripted 500×2 then 200 → status Retrying → Sent;
   and permanent-failure variant → Failed → manual Retry succeeds.

Provider doubles: WireMock mappings checked into `tests/providerdoubles/` and shared
between integration and E2E (single source of provider behavior truth). Recorded real
sandbox responses refresh these fixtures quarterly (manual task).

## Non-functional

- **Load smoke:** k6 script — 50 concurrent users browsing + 10 sends/min for 10 min;
  p95 API < 300 ms, zero 5xx.
- **Security tests in CI:** dependency audit (dotnet + npm), sanitizer corpus is the
  regression net for XSS; ZAP baseline scan against staging weekly.
- **Manual pre-release:** real-account matrix — one real Gmail + one real M365 sandbox
  send per release; visual check in actual Gmail web/Outlook desktop for the canonical
  templates (documented checklist, since client-approximation preview is not proof).
