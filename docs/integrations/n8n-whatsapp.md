# WhatsApp → AI → approval → send (n8n)

Automate email creation and sending from WhatsApp: a user sends a prompt (and
optionally image URLs) to a WhatsApp number, the AI drafts a template, the draft
is sent back for approval, and on approval the agent sends the email — all
orchestrated by [n8n](https://n8n.io).

```
WhatsApp ──▶ n8n webhook ──▶ POST /ai/templates/generate ──▶ POST /templates
   ▲                                                             │
   │  preview + "reply APPROVE"                                  ▼
   └────────────────── Wait (approval) ◀── send preview to WhatsApp
                          │
                          ▼ (approved)
                    POST /sends ──▶ email delivered ──▶ WhatsApp confirmation
```

## 1. Run n8n

n8n is an opt-in service in `docker-compose.yml`:

```bash
docker compose --profile automation up -d n8n
```

Open http://localhost:5678. Inside the container the API is reachable at
`http://host.docker.internal:5001/api/v1` (exposed as `MTH_API_BASE`).

## 2. Create an API key

In the app: **Settings → API keys → Create key**. Copy the `mth_…` secret (shown
once). Every API call below authenticates with it:

```
Authorization: Bearer mth_xxxxxxxx…
```

API keys can call automation endpoints (generate / create / send) but **cannot**
manage keys — key management stays browser-session only.

## 3. Import the workflow

Import `docs/integrations/n8n-workflow.json` into n8n. Set two credentials/vars:

- **MTH_API_KEY** — the `mth_…` secret from step 2.
- **WhatsApp provider** — wire the two WhatsApp nodes to your provider
  (Twilio / Meta WhatsApp Cloud). They are generic HTTP nodes so any provider
  fits; the inbound webhook just needs to deliver `{ prompt, recipient, from }`.

## 4. The endpoints the agent uses

### Generate a draft
```http
POST /api/v1/ai/templates/generate
Authorization: Bearer mth_…
Content-Type: application/json

{ "prompt": "Black Friday 40% off for our VIP customers", "brandColor": "#111827" }
```
Returns `{ subject, mjmlSource, htmlBody, previewHtml, variables, aiGenerated }`.
`previewHtml` is the fully rendered, sanitized email — send it (or a screenshot of
it) back to WhatsApp for approval.

> With no `Ai:ApiKey` configured the server uses a deterministic scaffold. Set an
> Anthropic key (`Ai__ApiKey` env var) for real generative drafts.

### Persist it as a template
```http
POST /api/v1/templates
{ "name": "BF VIP", "content": { "editorKind": "mjml", "subject": "…", "mjmlSource": "…", "htmlBody": "", "variables": […], "assets": [] } }
```
Returns the template with `currentVersion.id`.

### Send after approval
```http
POST /api/v1/sends
{ "connectedEmailAccountId": "…", "templateVersionId": "…",
  "recipients": [ { "email": "vip@example.com" } ], "variables": {} }
```
Returns the send job; poll `GET /api/v1/sends/{id}` for `status: sent`.

Get the sending account id once with `GET /api/v1/email-accounts` (use the
`active` one). Idempotency: pass `Idempotency-Key: <whatsapp-message-id>` on
`POST /sends` so a retried WhatsApp delivery never double-sends.

## Approval gate

The workflow uses an n8n **Wait** node that pauses until its resume URL is hit.
Send that resume URL to WhatsApp alongside the preview (or map an "APPROVE" reply
to it). Only after resume does the workflow call `POST /sends`.

## Notes & limits

- Real sending needs a connected Gmail/Outlook account and (for Gmail) Google's
  `gmail.send` verification — see the project README.
- The AI endpoint is rate-limited (20/hour/IP by default).
- Everything the agent does is recorded in **Settings → Activity** (audit log).
