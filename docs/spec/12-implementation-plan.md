# 12 — Implementation Plan & Build Plan

Milestones are vertical slices — each ends with something demoable and tested.
Task ids `P<phase>.<n>` are referenced in commits/PRs. Estimates assume 1–2 engineers.

## Phase 0 — Foundations (week 1)

| Task | Definition of done |
|---|---|
| P0.1 Repo hygiene | `git init`, .gitignore (bin/obj/node_modules), `.editorconfig`, CI skeleton (build+test on PR) |
| P0.2 docker-compose | Postgres 16 + MinIO (+ console) + WireMock; `README` quickstart runs on a clean machine |
| P0.3 Solution wiring | Project refs per 03 §2; remove `Class1.cs`; NetArchTest project enforcing layer rules |
| P0.4 API baseline | Serilog, ProblemDetails middleware, health endpoint `/healthz`, OpenTelemetry console exporter, options-pattern config with `ValidateOnStart` |
| P0.5 EF Core baseline | `AppDbContext`, snake_case convention, citext migration 0, interceptors (timestamps), Testcontainers integration-test harness green |
| P0.6 Frontend scaffold | `frontend/` Next.js TS strict + Tailwind + shadcn/ui + TanStack Query + `/api` rewrite proxy; CI lint/typecheck/test |

## Phase 1 — App auth + user model (week 2)

| Task | DoD |
|---|---|
| P1.1 Users/sessions schema + migrations | Tables per 05; arch/integration tests |
| P1.2 Register/login/logout/sessions API | 06 §1–2 incl. cookies, CSRF middleware, rate limiter policies; integration suite green |
| P1.3 Password reset | Tokens hashed, single-use; email delivery stubbed to log in dev (no provider dependency) |
| P1.4 Frontend auth pages + session guard | Login/register/reset pages, `(app)` layout guard, settings page (profile/password/sessions) |
| P1.5 Audit log write path | `IAuditWriter` + auth events recorded |
| **Gate** | E2E: register→login→settings→logout. **Start Google OAuth verification paperwork now (external lead time!)** |

## Phase 2 — Provider connections (weeks 3–4)

| Task | DoD |
|---|---|
| P2.1 Token cipher | AES-GCM envelope + KEK versioning + tests (roundtrip/tamper/rotate) |
| P2.2 OAuth state service + start/callback endpoints | 04 §2 flow; WireMock integration tests for happy/expired/reused/scope-missing |
| P2.3 Google connect | Dev console registration, `GoogleOAuthService`, profile fetch, account upsert |
| P2.4 Microsoft connect | Entra registration, `/common`, tenant metadata, refresh rotation persistence |
| P2.5 ITokenRefreshService | Advisory-lock refresh, invalid_grant → needs_reconnect, provider events |
| P2.6 Accounts API + UI | List/default/test/disconnect endpoints; /accounts page with connect flows, error explainer states |
| **Gate** | E2E journeys 1–2 (connect Gmail/Outlook vs doubles); manual connect with real test accounts |

## Phase 3 — Assets (week 5)

| Task | DoD |
|---|---|
| P3.1 IObjectStorage + presign | S3ObjectStorage vs MinIO; bucket layout private/public/snapshots |
| P3.2 Upload endpoints + verification | presign→complete with magic-byte/MIME/size/checksum/dedupe/quota; audit |
| P3.3 Assets API (list/download-url/visibility/delete + in-use 409) | |
| P3.4 Asset library UI + FilePond dropzone | Grid, filters, detail sheet, delete-in-use dialog |
| **Gate** | E2E journey 4 (upload GIF); rejection paths verified |

## Phase 4 — Templates & rendering (weeks 6–8, longest phase)

| Task | DoD |
|---|---|
| P4.1 Template/version schema + CRUD API | 06 §5–6 incl. duplicate/archive/restore, name uniqueness, immutability |
| P4.2 Rendering pipeline | Mjml.Net, sanitizer, Handlebars, PreMailer, text generator, asset resolver; golden files + XSS corpus green |
| P4.3 Preview/validate endpoints | sample/strict, warnings catalog |
| P4.4 Template list UI | |
| P4.5 Editor shell: MJML source tab + preview pane | CodeMirror, debounced validate, sandboxed iframe, desktop/mobile |
| P4.6 GrapesJS visual tab | grapesjs-mjml, custom block set, asset picker integration, hosted/CID choice, variable chips |
| P4.7 Versioning UX | Save version, history sheet, restore, conflict dialog |
| P4.8 Import/export | Export HTML; import MJML/HTML(-as-mj-raw) |
| **Gate** | E2E journeys 3 + 5; golden-render parity check vs reference mjml output |

## Phase 5 — Sending (weeks 9–10)

| Task | DoD |
|---|---|
| P5.1 Send job schema + POST /sends validation | Budget math, per-recipient variable resolution, idempotency key |
| P5.2 MimeKitEmailMessageBuilder | Structure tests (±CID ±attachments), size accounting |
| P5.3 Hangfire + SendEmailJob | Claim loop, state machine, retries/backoff/Retry-After, reaper, snapshot persist |
| P5.4 Gmail client | Raw send + resumable > 5 MB; error map; throttle |
| P5.5 Graph client | sendMail ≤ 3 MB / draft+upload-session path; error map; Retry-After honor |
| P5.6 Test send | Endpoint + editor dialog; parity with real path |
| P5.7 History UI + job detail | Tables, polling, cancel/retry, snapshot viewer |
| **Gate** | E2E journeys 6 + 8; manual real-account send matrix (Gmail + M365) |

## Phase 6 — Scheduling, audit UI, hardening (week 11)

| Task | DoD |
|---|---|
| P6.1 Scheduled sends | scheduledAt on POST, promoter job J3, reschedule/cancel, scheduled UI |
| P6.2 Token refresh sweep J4 + cleanup J5 | Recurring registration, metrics |
| P6.3 Audit UI + dashboard page | |
| P6.4 Security pass | Header audit, rate-limit tuning, ZAP baseline, dependency audit, pen-check of ownership sweep (11-testing) |
| P6.5 Observability | OTLP exporter config, alert rules from 10 §Observability, Serilog scrubbing verification |
| **Gate** | E2E journey 7; load smoke; full regression |

## Phase 7 — Launch readiness (week 12)

| Task | DoD |
|---|---|
| P7.1 Prod infra | Managed PG (backups/PITR), S3 + CDN for public assets, KMS-backed KEK, secret manager |
| P7.2 Deploy pipeline | Migration step, blue/green or rolling, smoke tests post-deploy |
| P7.3 Google verification follow-through | Restricted-scope review submitted with demo video + privacy policy; **launch to Google users blocked on this — run closed beta (≤100 test users) meanwhile** |
| P7.4 Docs | User-facing: scopes/privacy explainer; ops runbook: needs_reconnect storms, provider outage, KEK rotation |

## Post-MVP backlog (ordered)

1. Contacts/groups UI + compose integration (schema ready)
2. Drafts UI with autosave (schema ready)
3. Dynamic sections UX (`{{#if}}/{{#each}}` visual blocks)
4. Provider sync J7 (separate consent) — message-id backfill, bounce detection
5. React Email as an alternative dev-oriented engine (fits behind `editor_kind='react-email'` + a Node render sidecar — the `IMjmlCompiler`-style port pattern already exists)
6. Organizations/teams (add `OrganizationId`, role checks)
7. Convert-inline-to-hosted retry helper; image optimization (resize/compress on upload)

## Cross-phase risk register

| Risk | Watch at | Mitigation |
|---|---|---|
| Google restricted-scope review delay | P1 onward | Started at P1 gate; beta under test users |
| Mjml.Net fidelity gap | P4.2 | Golden diffs vs reference; sidecar swap documented (08 §2.3) |
| Graph sendMail lacks message id | P5.5 | Ref header + draft-path for large; sync job post-MVP |
| Hangfire double-execution | P5.3 | Recipient-row claim + reaper design already assumes at-least-once |
| MJML editor/GrapesJS bundle size | P4.6 | dynamic import, route-level code split, measure in CI (bundle budget 350 KB gz for editor route acceptable) |
