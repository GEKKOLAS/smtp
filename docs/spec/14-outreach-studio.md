# 14 — Outreach Studio (phase 1 of the Developer & Creative Toolkit)

**Version:** 0.3 · **Date:** 2026-10-30 · **Status:** F1–F4 implemented on `feat/outreach-studio`;
the phone timelines (F4.4, last bullet) are still to do

Mail Template Hub is phase 1 ("Outreach Studio") of a larger ecosystem whose next modules
are JobHunter OS and Application Copilot. Most of Outreach Studio already exists (visual
MJML builder, immutable versions, multi-width preview, HTML import/export, AI template
generation). This document specifies the four missing pieces, at the "professional to
enterprise" bar the rest of the spec set uses: migrations, audit, tests and both themes.

| # | Feature | Today | This spec |
|---|---------|-------|-----------|
| F1 | Brand Kit | — | Kits with colors, fonts, logo, button style, voice; design-token export; brand blocks in the editor; AI generation uses the kit |
| F2 | AI Copy Assistant | Whole-template generation only | Tone-based variations for subject, preheader, text blocks and CTAs |
| F3 | Version compare | List + restore | Visual, code and field diff between any two versions |
| F4 | Campaign analytics | Per-recipient send status | Opt-in click tracking, UTM tagging, bounce detection, analytics page |

## Amendments to earlier specs

- **01-prd §5 (non-goals):** "no open/click tracking pixels in v1" becomes: *no open-tracking
  pixels; click tracking is opt-in per send (F4).* Opens stay out — Apple Mail Privacy
  Protection and Gmail's image proxy make them noise, and a pixel is the more invasive
  mechanism. Mass-marketing limits (recipient cap, no purchased lists) are unchanged.
- **05-database / 06-api / 10-jobs:** the tables, endpoints and job below are additive; they
  are listed here and folded into those documents when each feature lands.

## F1 — Brand Kit

### Data

`brand_kits` (user-owned, max 10 per user, exactly one `is_default` per user when any exist):

| Column | Type | Notes |
|--------|------|-------|
| id, user_id | uuid | Ownership filter like every user-owned table |
| name | text ≤ 80 | |
| is_default | bool | Partial unique index on (user_id) where is_default |
| colors | jsonb | `primary, secondary, background, surface, text, muted, link` — `#RRGGBB` only |
| heading_font, body_font | text | From an allowlist; each maps to an email-safe fallback stack |
| logo_asset_id | uuid? | FK assets; must be the user's own image asset, made public on save |
| button | jsonb | `radius` (0–32 px), `style` (`filled` \| `outline`) |
| radius | jsonb | `small, medium, large` (0–40 px) — design tokens |
| spacing | jsonb | `small, medium, large` (0–64 px) — design tokens |
| voice | text ≤ 500 | Tone of voice, fed to F2 and to AI generation |
| created_at, updated_at | | Last write wins: a kit has a single editor |

### API

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/v1/brand-kits` | `{items}` |
| POST | `/api/v1/brand-kits` | First kit becomes default |
| GET | `/api/v1/brand-kits/fonts` | Font allowlist: `key, label, stack, googleFamily` |
| GET / PUT / DELETE | `/api/v1/brand-kits/{id}` | Deleting the default promotes the newest remaining kit; 10 kits max (409 `brand_kit.limit`); a logo that isn't the user's image → 409 `brand_kit.invalid_logo` |
| POST | `/api/v1/brand-kits/{id}/default` | |
| GET | `/api/v1/brand-kits/{id}/tokens?format=json\|css` | Design tokens (`colors`, `spacing`, `radius`, `typography`); CSS emits `--brand-*` custom properties |

Audit: `brand_kit.created`, `brand_kit.updated`, `brand_kit.deleted`.

### Uses

- **Editor brand blocks:** the GrapesJS block manager gets a "Brand" category built from the
  default kit — header (logo), button (primary + kit radius/style), card (surface + radius),
  footer (muted text, small logo). Blocks are plain MJML with literal values, so a template
  never depends on the kit after insertion (changing the kit doesn't silently restyle sent
  history).
- **AI generation:** `GenerateRequest.BrandKitId` (defaults to the default kit). The kit
  replaces the single `brandColor` input: exact palette, fonts, logo URL, button style and
  voice go into the prompt; the scaffold generator uses the same palette.
- **Fonts:** templates get the font *stack* only, with the web font's family first. No
  `@import`: Outlook desktop and many Gmail apps ignore web fonts anyway, and the stack
  degrades cleanly everywhere. The `/brand` preview loads the web font to show intent.
- **Found while building:** the sanitizer's CSS parser expands `border`, `border-radius`
  and `background` into longhands before checking its allowlist, so every rounded or
  bordered button and every hero background placement was being stripped from all
  templates. The longhands are now allowed (`GanssHtmlSanitizer`, with unit tests).

### UI

`/brand` page (nav: sidebar + phone account menu): kit list, editor with color inputs,
font selects, logo picker from Assets, button preview, voice field, a live sample email
card, "Export tokens" (JSON / CSS download), "Set as default".

## F2 — AI Copy Assistant

`POST /api/v1/ai/copy/variations` (rate limit `ai`):

```json
{ "kind": "subject|preheader|body|cta", "text": "…", "tone": "promotional|professional|technical|followUp",
  "count": 3, "brandKitId": null, "context": "optional: template subject or purpose" }
```

Response `{ "variations": [{ "text": "…", "note": "what changed" }], "model": "…" }`.

- Port `IAiCopyWriter` (Application) with an Anthropic implementation (non-streaming,
  small `max_tokens`, `AiOptions.Model`). No scaffold fallback — fake rewrites are worse
  than none; `GET /ai/capabilities` gains `copyAssistant` and the UI hides the feature.
- Limits: `text` ≤ 200 chars for subject/preheader/cta, ≤ 2,000 for body; `count` 1–5.
- **Guardrails, enforced server-side:** every variation must contain exactly the same set
  of `{{placeholders}}` as the input (variations that drop or invent one are discarded;
  all discarded → 422 `ai.generation_failed`). The system prompt keeps the source language,
  forbids adding facts, numbers, prices, dates or claims absent from the source, and
  avoids spam patterns (ALL CAPS, `!!!`, "free" bait). Brand voice applies when a kit is given.
- **UI:** a ✨ button on the subject and preheader fields of the editor's inbox bar, and a
  "Rewrite" toolbar action on a selected text/button component in GrapesJS. Both open the
  same panel: tone chips → variations → click to apply (one undo step). Components with
  inline markup (links, bold) are rewritten as plain text only after a warning.

## F3 — Version compare

Frontend only: `GET /templates/{id}/versions/{versionId}` and `POST /render/preview` with
`templateVersionId` already provide everything.

- Version history gets "Compare": pick two versions (default: current vs previous).
- Full-screen compare dialog, three tabs:
  - **Visual** — both versions rendered with sample values in sandboxed iframes, side by
    side on desktop (A/B toggle on phones), 640/375 width switch, synced scrolling.
  - **Code** — CodeMirror merge view (`@codemirror/merge`) over the MJML source, or the
    HTML body for HTML templates, unchanged regions collapsed.
  - **Fields** — subject, preheader, editor kind, plain-text body, and variables
    added / removed / changed (type, default, required).
- "Restore vN" from either side uses the existing restore endpoint.
- The rendered frames are script-free (`sandbox="allow-same-origin"`, no `allow-scripts`)
  and sized to their content, so both versions scroll together.
- **Found while building:** template versions were saved without a creation time
  (`0001-01-01`). New versions are stamped by `TemplateVersionFactory`; older rows show
  "date unknown" rather than a made-up date.

## F4 — Campaign analytics

### Honest metrics

| Metric | Definition |
|--------|------------|
| Accepted | Recipient rows the provider accepted (`Sent`). Not called "delivered" |
| Bounced | Accepted recipients matched to a bounce notification (F4.3) |
| Failed | Rejected by the provider at send time |
| Unique clickers | Recipients with ≥ 1 human click on a tracked link |
| Click rate | Unique clickers ÷ (accepted − bounced), tracked sends only |

No open rate. Conversions are measured in the user's own analytics through UTM tags.

### F4.1 Click tracking and UTM

- `email_send_jobs` gains `track_clicks bool` (default false) and `utm_campaign text?`
  (≤ 100 letters, digits, spaces, `. _ -`). Compose gets a "Track clicks" toggle and a
  "UTM campaign" field. Replies stay untracked: a reply is a conversation, not a campaign.
- New config `Tracking:PublicBaseUrl`. When unset, `track_clicks: true` → 422
  `tracking.unavailable` and the UI shows the toggle disabled with the reason (recipients
  can't reach `localhost`).
- `LinkRewriter` runs in `EmailSendService` **after** rendering (so hrefs are final and
  sanitized), per recipient:
  - rewrites absolute `http(s)` hrefs; skips `mailto:`, `tel:`, `#…`, `cid:`, and any link
    whose URL or text mentions unsubscribe;
  - appends `utm_source=mail-template-hub&utm_medium=email&utm_campaign=…` when set, never
    overriding existing `utm_*` parameters (UTM works without click tracking too);
  - stores one `tracked_links` row per distinct link per recipient and replaces the href
    with `{PublicBaseUrl}/api/v1/t/{token}`; the plain-text part gets the same replacement.
  - The rendered snapshot keeps the untracked HTML.
- `tracked_links`: id, send_job_id, recipient_id, token (16 random bytes, base64url, unique),
  target_url (the link plus any UTM tags), position, created_at. A retry reuses the
  recipient's existing links. `link_clicks`: id, tracked_link_id, occurred_at,
  classification (`human` \| `scanner`). No IP address or raw user agent is stored.

### F4.2 Redirect endpoint

`GET /api/v1/t/{token}` — anonymous, rate limit `tracking` (per IP).

- Looks the token up and 302s to the stored `original_url`. The destination never comes
  from the request, so it is not an open redirect. Unknown token → 404 page.
- Headers: `Cache-Control: no-store`, `Referrer-Policy: no-referrer`.
- `HEAD` returns the redirect without recording a click.
- Classification `scanner` when the user agent matches known link scanners (Outlook Safe
  Links, Proofpoint, Mimecast, Barracuda, curl/python clients…) or the click lands within
  10 s of the recipient's send time. Scanner clicks are kept but excluded from the metrics.
- Retention: `CleanupJob` deletes clicks older than `Tracking:RetentionDays` (default 365).

### F4.3 Bounce detection (job J6)

- `RecipientStatus.Bounced` (statuses are stored as strings — no data migration) plus
  `bounced_at`, `bounce_code`, `bounce_reason` on recipients.
- `DetectBouncesJob`, every 15 min, for accounts that have recipients sent in the last 72 h
  and inbox-read consent: find delivery notifications (new `IInboxProviderClient` method
  `FindDeliveryNotificationsAsync` — Gmail `from:(mailer-daemon OR postmaster)
  newer_than:3d`, Graph `subject:Undeliverable OR from:postmaster OR from:mailer-daemon`),
  fetch each new one as raw MIME (`GetRawMessageAsync`: Gmail `format=raw`, Graph
  `/$value`), parse it with MimeKit (`message/delivery-status` + the returned original
  headers, or the reference pasted in the text part).
- A notification counts only if `Action: failed` with a `5.x.x` status (hard bounce;
  delayed/4.x.x are ignored) and the original `X-MailTemplateHub-Ref` header resolves to a
  recipient of a job on **the same account**. Processed message ids are remembered in
  `processed_inbox_notices` (kept 30 days) so a notification is applied once.

### F4.4 Analytics API and UI

| Method | Path | Returns |
|--------|------|---------|
| GET | `/api/v1/analytics/summary?from&to` | Totals, per-day series, per-template rows, top links |
| GET | `/api/v1/sends/{id}/analytics` | Per-link clicks, per-recipient clicked/bounced, click timeline |

- `/analytics` page: 7/30/90-day range, KPI tiles, sends-per-day chart (accepted / failed /
  bounced), per-template table (sends, recipients, bounce rate, click rate), top links.
- Send detail gains a results card (accepted, bounced, click rate, per-link clicks) and
  bounce/clicked badges per recipient.
- *To do:* the phone Activity timeline (web and the Expo app) showing bounces and clicks.

## Testing

- **Unit:** link rewriter (skips, UTM merge, idempotence, text part), placeholder guard,
  bot classifier, DSN parser with Gmail and Outlook fixtures, brand-kit validation, token
  export shape, font fallback rendering.
- **Integration (Testcontainers):** brand-kit CRUD, default switching and cross-user
  isolation; copy endpoint with a fake writer; tracked send → links created, `/t/{token}`
  302 + click recorded, unknown token 404, HEAD not counted; `tracking.unavailable`; bounce
  job with a fake inbox client; analytics numbers.
- **Frontend:** vitest for field diff and analytics math; build, lint, typecheck;
  Playwright screenshots of every new screen in Mailroom and Nocturne.

## Delivery order

Each step is one reviewable change; tests and both themes are part of "done".

1. This spec + PRD amendment.
2. F1 Brand Kit — backend, `/brand` page, token export.
3. F1 uses — brand blocks in the editor, kit in AI generation.
4. F2 AI Copy Assistant.
5. F3 Version compare (frontend only; independent of 2–4).
6. F4.1 + F4.2 click tracking, UTM, redirect endpoint.
7. F4.3 bounce detection job.
8. F4.4 analytics API, `/analytics` page, send detail, phone timelines.
9. Screenshots, README, spec status.

## Risks

| Risk | Mitigation |
|------|------------|
| Link scanners inflate clicks | Scanner classification + 10 s window; metrics exclude them |
| Tracking needs a public host | `Tracking:PublicBaseUrl` gate; disabled with an explanation until deployed |
| DSN formats vary by provider | Parse standard `message/delivery-status`; fixtures from Gmail and Outlook; unmatched notifications are ignored, never guessed |
| Web fonts in email | Always emit fallback stacks; web font only if a test proves it survives compile + sanitize |
| AI rewrites that change meaning | Placeholder guard, no-new-facts prompt, user picks and can undo every change |
