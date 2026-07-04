# Mail Template Hub — Technical Specification

**Version:** 1.0 · **Date:** 2026-07-04 · **Status:** Draft for review

A SaaS-style web application for connecting Gmail and Outlook accounts, building reusable
email templates (visual + MJML), managing assets, and sending/scheduling tracked email
through provider APIs.

## Spec-driven workflow

This spec set is the source of truth. Nothing gets implemented that isn't specified here;
when reality forces a change, the spec is amended first (or in the same PR), then code.

```
PRD ──▶ User Stories ──▶ Architecture + Security ──▶ Contracts (DB / API / Providers)
                                                        │
                          Implementation Plan ◀── Frontend / Rendering / Jobs specs
```

## Documents

| # | Document | Deliverable | Contents |
|---|----------|-------------|----------|
| 01 | [01-prd.md](01-prd.md) | A | Product requirements, personas, MVP scope, non-goals |
| 02 | [02-user-stories.md](02-user-stories.md) | B | User stories with acceptance criteria |
| 03 | [03-architecture.md](03-architecture.md) | C, M | System architecture, provider abstraction, folder structure |
| 04 | [04-security.md](04-security.md) | D | Auth model, token encryption, OAuth hardening, sanitization, SSRF, audit |
| 05 | [05-database.md](05-database.md) | E | Full PostgreSQL / EF Core schema, indexes, ownership model |
| 06 | [06-api.md](06-api.md) | F | REST API: every endpoint with request/response/validation/authz |
| 07 | [07-providers.md](07-providers.md) | G | Gmail API + Microsoft Graph integration, token refresh, MIME & attachments |
| 08 | [08-rendering.md](08-rendering.md) | H | Template rendering pipeline: MJML, sanitize, variables, CID, plain text |
| 09 | [09-frontend.md](09-frontend.md) | I | Next.js pages/components, editor UX, states, permissions |
| 10 | [10-jobs.md](10-jobs.md) | J | Background jobs: triggers, payloads, idempotency, retries |
| 11 | [11-testing.md](11-testing.md) | K | Unit / integration / E2E strategy, tooling |
| 12 | [12-implementation-plan.md](12-implementation-plan.md) | L | Milestones, phases, task breakdown, build plan |
| 13 | [13-code-skeletons.md](13-code-skeletons.md) | N | Key backend & frontend module skeletons |

## Locked technology decisions

| Concern | Choice | Why |
|---------|--------|-----|
| Backend | ASP.NET Core 8 Web API, Clean Architecture (existing `MailTemplateHub.*` projects) | Matches scaffold; testable seams for provider clients |
| ORM / DB | EF Core 8 + Npgsql / PostgreSQL 16 | Migrations, `jsonb`, `xmin` concurrency, `SKIP LOCKED` queues |
| Background jobs | Hangfire + Hangfire.PostgreSql | Dashboard, per-job retries, no extra broker; DB already required |
| MIME | MimeKit | The de-facto .NET MIME builder; correct CID/multipart handling |
| Gmail | Google.Apis.Gmail.v1 (`messages.send`, raw RFC 2822) | Send raw MimeKit output; one MIME path for both providers |
| Outlook | Microsoft.Graph SDK (`sendMail` / draft + upload session) | Official SDK; large-attachment upload sessions |
| MJML compile | Mjml.Net (in-process .NET port) | No Node sidecar for MVP; sidecar documented as fallback |
| Sanitization | Ganss.Xss HtmlSanitizer | Allowlist-based, actively maintained |
| CSS inlining | PreMailer.Net | Standard .NET inliner for email HTML |
| Token crypto | AES-256-GCM envelope encryption (`ITokenCipher`), keys via env/KMS | Versioned keys, rotation without re-consent |
| Storage | MinIO (dev) / any S3 API (prod) via `IObjectStorage` on AWSSDK.S3 | Single abstraction, presigned URLs |
| Frontend | Next.js 14 App Router, TS strict, Tailwind + shadcn/ui | Server/client split, accessible primitives |
| Data fetching | TanStack Query v5 | Cache, retries, optimistic updates for editor |
| Forms/validation | react-hook-form + Zod | Shared schema types with API DTOs |
| Editor | GrapesJS + `grapesjs-mjml` | Visual builder that emits MJML → responsive HTML |
| Uploads | FilePond (+ image preview/validate plugins) | Chunked/presigned upload UX |
| Logging | Serilog (structured) → OpenTelemetry OTLP | Correlated traces across API + jobs |

## Glossary

| Term | Meaning |
|------|---------|
| **Connected account** | A Gmail or Microsoft mailbox linked via OAuth, owned by exactly one app user |
| **Template** | Named, user-owned design container; holds metadata + pointer to current version |
| **Template version** | Immutable snapshot of subject/preheader/MJML/HTML/GrapesJS JSON/variables |
| **Send job** | One send operation (1..n recipients) with lifecycle Queued → … → Sent/Failed |
| **Rendered snapshot** | Final HTML+text produced for a send, stored in object storage for audit |
| **CID asset** | Image embedded in the MIME message and referenced by `cid:` URI |
| **Hosted asset** | Image referenced by public HTTPS URL served from object storage/CDN |
