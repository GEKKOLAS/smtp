# Mail Template Hub

Connect Gmail and Outlook via OAuth, build responsive email templates (GrapesJS + MJML),
manage assets, and send/schedule tracked email through the providers' own APIs.

**Spec-driven:** the full technical specification lives in [docs/spec/](docs/spec/README.md).
The build follows [docs/spec/12-implementation-plan.md](docs/spec/12-implementation-plan.md).

## Stack

ASP.NET Core (.NET 10) · EF Core + PostgreSQL · Hangfire · MimeKit · Mjml.Net ·
MinIO/S3 · Next.js App Router (TypeScript) · Tailwind + shadcn/ui · TanStack Query · GrapesJS

## Quickstart (local dev)

Prereqs: .NET 10 SDK, Node 20+, Docker.

```bash
# 1. Infrastructure: PostgreSQL 16, MinIO (S3), WireMock provider doubles
docker compose up -d

# 2. Backend API → http://localhost:5001  (health: /healthz)
dotnet run --project MailTemplateHub.Api

# 3. Frontend → http://localhost:3000 (proxies /api/* to the backend)
cd frontend && npm install && npm run dev
```

MinIO console: http://localhost:9001 (minioadmin / minioadmin).
Buckets `mth-private`, `mth-public` (anonymous download), `mth-snapshots` are created automatically.

## Tests

```bash
dotnet test                        # unit + architecture + integration (integration needs Docker)
cd frontend && npm test            # vitest
```

## Solution layout

| Project | Role |
|---|---|
| `MailTemplateHub.Domain` | Entities, enums, domain errors — no dependencies |
| `MailTemplateHub.Application` | Use cases, DTOs, validators, ports (interfaces) |
| `MailTemplateHub.Infrastructure` | EF Core, provider clients, storage, crypto, jobs |
| `MailTemplateHub.Api` | Controllers, middleware, auth, composition root |
| `frontend/` | Next.js app |
| `tests/` | Unit, integration (Testcontainers), architecture tests |
