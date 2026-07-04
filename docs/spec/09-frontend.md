# 09 — Frontend Specification (Next.js App Router)

Stack: Next.js 14 App Router · TypeScript strict · Tailwind + shadcn/ui · TanStack Query
v5 · react-hook-form + Zod · FilePond · GrapesJS (+`grapesjs-mjml`) · CodeMirror 6 for
MJML source. Accessibility: shadcn/Radix primitives, focus management in dialogs,
`aria-live` for async status, WCAG AA contrast. All pages responsive ≥ 360 px.

**Global patterns**

- Route groups: `(auth)` public, `(app)` behind session guard (server component checks
  `GET /me`, redirects to `/login?next=`).
- API access: fetch wrapper adds `X-CSRF-Token`, maps 401 → redirect, 422 → field errors,
  ProblemDetails → typed `ApiError`. TanStack Query keys in `lib/query/queryKeys.ts`.
- Loading = skeletons (`loading.tsx` per route); errors = `error.tsx` boundary with retry;
  empty states always have a primary CTA; destructive actions use confirm dialogs.
- Permissions: single-user app ⇒ "permission" = authenticated + resource ownership
  (server-enforced); UI additionally disables actions that will 409 (e.g. send with
  `needs_reconnect` account) with an explanatory tooltip.

## Pages

### /login · /register · /reset-password `(auth)`
| | |
|---|---|
| Purpose | Session establishment; anti-enumeration copy |
| Components | `AuthCard`, RHF+Zod forms, password strength meter (zxcvbn-lite), rate-limit banner |
| API | `POST /auth/login` · `/register` · `/password/forgot` · `/password/reset` |
| States | Submitting spinner on button only; 401 → inline "invalid email or password"; 429 → countdown banner; reset link expired → error card with re-request CTA |

### /dashboard
| | |
|---|---|
| Purpose | At-a-glance: connected accounts health, recent sends, quick actions |
| Components | `AccountHealthStrip` (reconnect CTAs), `RecentSendsTable` (last 10, status badges), `QuickActions` (New template / Compose / Connect account), `ScheduledUpcoming` |
| API | `GET /email-accounts`, `GET /sends?page=1&pageSize=10`, `GET /sends?status=scheduled` |
| Empty | First-run hero: 3-step checklist (Connect → Create template → Send test) |
| Error | Per-widget error cards; page never hard-fails on one widget |

### /accounts (Connected accounts + connect flows)
| | |
|---|---|
| Purpose | List/connect/disconnect/set-default; landing for OAuth redirects |
| Components | `AccountCard` (provider icon, email, state badge `active/needs_reconnect/revoked`, default star, Test/Disconnect menu), `ConnectButtons` (Gmail/Outlook), `DisconnectDialog` (warns about queued sends), consent-explainer sheet ("we request send-only access") |
| API | `GET /email-accounts` · `GET /oauth/{provider}/start` → `window.location = authorizationUrl` · `POST /{id}/default` · `POST /{id}/test` · `DELETE /{id}` |
| Flow states | Reads `?connected=gmail` / `?error=oauth.scope_missing` query on mount → success toast or error dialog with targeted copy (scope denied ⇒ "you unchecked send permission, reconnect and allow it"); MS admin-consent error ⇒ dedicated explainer |
| Empty | Both provider buttons large, with scope explanation |
| Permissions | Disconnect requires confirm; test button disabled while account `revoked` |

### /templates (list)
| | |
|---|---|
| Purpose | Browse/manage templates |
| Components | `TemplateGrid` (name, updatedAt, version count, archived badge), search input (debounced), archived filter tab, row menu (Edit/Duplicate/Archive/Delete), `NewTemplateDialog` (name + start from: blank visual / blank MJML / import) |
| API | `GET /templates` (query-keyed by search/filter/page) · `POST /templates` · `/duplicate` · `/archive` · `DELETE` |
| States | Skeleton grid; empty → "Create your first template" CTA; delete confirm shows send-history note |

### /templates/[id]/edit — Editor shell (largest surface)
| | |
|---|---|
| Purpose | Visual + source editing, preview, versioning, test send |
| Layout | Top bar: name (inline edit), version badge, autosave state ("Saved · v7" / "Unsaved changes"), actions (Preview, Test send, Save version, ⋮: Duplicate/Export HTML/Import/Version history). Tabs: **Visual** · **MJML** · **Preview**. Right rail: `VariablePanel`, `AssetPanel` |
| Components | `GrapesEditor` (client-only `next/dynamic`, `ssr:false`), `MjmlSourceEditor` (CodeMirror, MJML lint from `POST /render/validate` debounced 500 ms), `PreviewPane` (sandboxed iframe, desktop 600 px / mobile 375 px toggle + Gmail/Outlook approximation toggle — applies client CSS constraint emulation: strips unsupported styles per client profile), `VariablePanel` (detected vars, type/required/sample editing), `TestSendDialog` (account picker + sample vars), `VersionHistorySheet` (list, preview, restore), `AssetPickerDialog` |
| API | `GET /templates/{id}` · `POST /templates/{id}/versions` (save) · `POST /render/preview` · `POST /render/validate` · `POST /sends/test` · versions list/restore · assets list |
| Editor↔data contract | Visual tab edits GrapesJS project; on save: `editor.getMjml()` → send grapesProject + mjmlSource; MJML tab edits source directly (visual JSON marked stale per US-TPL-2) |
| States | Dirty-tracking + `beforeunload` guard; save conflict (409 version_conflict) → "reload latest / save as copy" dialog; MJML errors inline-annotated; preview error → warning list panel |
| GrapesJS blocks | Text, Button (VML-safe), Image, GIF (same block, gif badge), Columns (2/3), Divider, Spacer, Header, Footer, Social links row, **Variable chip** (inserts `{{name}}` inline token), Dynamic section (`{{#if}}` wrapper, post-MVP) |
| Image block options | source: asset library / upload; delivery: Hosted ⭘ Inline-CID ⭘ (with size hint); alt text required field (warning if empty) |

### /assets (Asset library)
| | |
|---|---|
| Purpose | Upload/manage media & files |
| Components | `UploadDropzone` (FilePond: presigned flow — `POST /assets/uploads` → PUT to storage → `complete`; image preview, type/size client pre-validation mirroring server rules), `AssetGrid` (thumbnail/type icon, size, kind filter tabs Images/GIFs/Docs/All, search), `AssetDetailSheet` (usage list, visibility toggle public/private, copy URL, delete) |
| API | `POST /assets/uploads` · `PUT (storage)` · `POST /uploads/{id}/complete` · `GET /assets` · `GET /{id}/download-url` · `POST /{id}/visibility` · `DELETE /{id}` |
| States | Per-file upload progress + per-file errors (type rejected, too large, verification_failed); delete of in-use asset → usage dialog (409 payload) |
| Empty | Dropzone hero with allowed types/sizes |

### /compose (Send wizard)
| | |
|---|---|
| Purpose | Account → template → recipients → variables → attachments → review → send/schedule |
| Components | `SendWizard` stepper: 1) `AccountSelect` (default preselected; needs_reconnect disabled+CTA) 2) `TemplatePicker` (grid + version pin option) 3) `RecipientEditor` (chips input with RFC validation, contact/group picker post-MVP, cap 50 counter, per-recipient variable override table) 4) `VariableForm` (generated from schema via Zod, sample prefill button) 5) `AttachmentPicker` (asset library + upload, running size budget bar /25 MB) 6) `ReviewStep` (server-rendered preview strict-mode, per-recipient toggle, schedule datetime picker with TZ display, Send/Schedule buttons) |
| API | accounts/templates/assets queries · `POST /render/preview (strict)` · `POST /sends` · draft autosave `POST/PATCH /drafts` (debounced) |
| States | Strict preview 422 → jump to variables step with per-recipient missing list; `send.too_large` → attachments step with breakdown; success → redirect `/sends/{id}` with live status; draft restored banner on return |

### /sends (History) · /sends/scheduled · /sends/[id]
| | |
|---|---|
| Purpose | History, scheduled management, job detail |
| Components | `SendsTable` (status badge, subject, account, template, recipients summary, created; filters: status/account/template/date), `ScheduledTable` (+ Reschedule/Cancel), detail page: job header, `RecipientTable` (per-recipient status, provider message id, failure reason, retry-failed button), `EventTimeline` (provider events), `SnapshotViewer` (iframe of stored snapshot) |
| API | `GET /sends` (+filters) · `GET /sends/{id}` (poll 3 s while queued/sending/retrying via TanStack `refetchInterval`) · `/cancel` · `/retry` · `PATCH /schedule` · `GET /{id}/snapshot` |
| States | Live-updating badges with `aria-live=polite`; cancel/retry optimistic with rollback; empty history → "Send your first email" CTA |

### /contacts (post-MVP UI)
Standard CRUD table + group management; import dialog (CSV, explicit consent copy). APIs from 06-api §10.

### /settings
| | |
|---|---|
| Purpose | Profile, password, sessions, sending preferences |
| Components | `ProfileForm`, `PasswordForm` (current+new, logout-others notice), `SessionsTable` (device/IP/last-seen, revoke), `SendDefaults` (default account shortcut, test-send target choice) |
| API | `GET/PATCH /me` · `POST /me/password` · `GET/DELETE /auth/sessions` |

### /audit (Audit/security page)
| | |
|---|---|
| Purpose | Read-only security event trail |
| Components | `AuditTable` (action badge, entity link where resolvable, IP, time), action-type filter, date range |
| API | `GET /audit-logs` |
| Empty | "Security events will appear here" |

## Editor UX acceptance details (Deliverable 11)

| Requirement | Implementation |
|---|---|
| Drag-and-drop blocks | GrapesJS block manager, custom MJML block set above |
| Preview desktop/mobile | PreviewPane widths 600/375, device chrome frames |
| Gmail/Outlook approximation | CSS-constraint profiles applied to preview DOM (strip `background-image`+rounded corners for Outlook profile; clip at 102 KB with visible marker for Gmail profile). Labeled "approximation" — not a Litmus replacement, risk documented |
| Send test email | TestSendDialog from editor top bar, uses current unsaved content via inline `content` preview→`POST /sends/test` with `templateVersionId` of a transparently auto-saved version (test sends always reference a persisted version for audit) |
| Save as draft vs Save version | Autosave keeps dirty editor state in localStorage (crash safety); explicit **Save version** persists v(n+1). No server "template draft" concept in MVP — versions are cheap |
| Duplicate / Export HTML | Menu actions; export downloads compiled+inlined HTML file |
| Import HTML/MJML | Import dialog: `.mjml` parsed natively; `.html` wrapped in `mj-raw` with fidelity warning banner |

## Zod/API type strategy

Single source: backend publishes OpenAPI (Swashbuckle); `openapi-zod-client` generates
`lib/schemas/api.gen.ts` in CI; hand-written Zod only for client-side-only forms.
Drift between spec and client fails the frontend build.
