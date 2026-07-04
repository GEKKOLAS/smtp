# 10 — Background Job Specification (Hangfire)

Hangfire + `Hangfire.PostgreSql`, queues: `sends` (workers: 4), `maintenance` (2),
`tokens` (2). Dashboard mounted at `/hangfire` behind auth + role check (dev: local only).
All jobs: structured log scope `{JobName, JobRunId, EntityId}`, OpenTelemetry activity,
`CancellationToken` from Hangfire shutdown.

**Global idempotency rule:** every job re-checks entity state from the DB first and
no-ops if the state machine already moved on. Hangfire's at-least-once delivery is the
assumption everywhere.

## J1 · SendEmailJob — send queued emails

| | |
|---|---|
| Trigger | Enqueued on `POST /sends` (immediate) or by J3 (scheduled promotion); re-enqueued by its own retry scheduling |
| Payload | `{ sendJobId }` — everything else read fresh from DB |
| Flow | Job row `queued→sending`; loop: claim next recipient `FOR UPDATE SKIP LOCKED` where `status='pending' AND (next attempt due)`; render (cached prepared HTML per job); refresh token; build MIME; provider send (07-providers §4); write recipient outcome + `EmailProviderEvent`; throttle per account (1/s Gmail, 1/2s Graph). When no claimable recipients remain → finalize: all sent ⇒ `sent`; some ⇒ `partially_failed`; none ⇒ `failed`; write snapshot to storage on first successful render; audit `send.completed/failed` |
| Idempotency | Recipient row is the unit: `sending` claim is transactional; a crashed worker leaves `sending` rows that a reaper (J1a sweep inside J3's tick) resets to `pending` after 10 min with attempt++ — provider double-send window is accepted and minimized by claim-then-send ordering + `X-MailTemplateHub-Ref` header for forensic dedupe |
| Retry policy | **Per recipient**, transient kinds only: attempts ≤ 5, delay = `max(RetryAfter, 30s·2^n + jitter(0–10s))` → 30s/1m/2m/4m/8m; recipient stays `pending` with `next_attempt_at`; job status `retrying` while any pending with future attempt. `QuotaExceeded` parks whole job +1 h (max 3 parks). Hangfire automatic retries **disabled** (`[AutomaticRetry(Attempts = 0)]`) — our state machine owns retries so backoff survives restarts |
| Failure policy | Permanent kinds fail the recipient immediately; `AuthRevoked` fails all remaining recipients + job, flags account `needs_reconnect`, audits. Exhausted attempts ⇒ recipient `failed(retries_exhausted)` |
| Logging | Per recipient: attempt #, kind on failure, provider status, elapsed; job summary line with counts |

## J2 · RetryFailedSendsJob — manual retry entry point

| | |
|---|---|
| Trigger | `POST /sends/{id}/retry` (user action) |
| Payload | `{ sendJobId }` |
| Flow | Guard status ∈ `failed|partially_failed`; reset only `failed` recipients → `pending`, attempt_count 0, clear failure; job → `queued`; enqueue J1; audit `send.retried` |
| Idempotency | State guard: second click while already `queued` ⇒ no-op 202 |

## J3 · PromoteScheduledSendsJob — scheduled sends + reaper

| | |
|---|---|
| Trigger | Recurring cron `* * * * *` (every minute) |
| Payload | none |
| Flow | (a) `UPDATE email_send_jobs SET status='queued' WHERE status='scheduled' AND scheduled_at <= now()` returning ids → enqueue J1 each. (b) Reaper: recipients stuck `sending` > 10 min → `pending`+attempt++; jobs stuck `sending` with no active Hangfire job → re-enqueue |
| Idempotency | The UPDATE…RETURNING is the atomic claim; duplicate cron ticks find nothing |
| Retry/Failure | Tick failures just log — next minute retries naturally; alert if 5 consecutive tick failures |

## J4 · RefreshTokensJob — proactive token refresh

| | |
|---|---|
| Trigger | Recurring cron `0 * * * *` (hourly) |
| Payload | none |
| Flow | Select active accounts with `access_token_expires_at < now()+30min` **and** (scheduled job within 2 h OR last_used_at < 7 d) → `ITokenRefreshService` each (advisory-locked). Also: accounts with `refresh_failure_count ≥ 1` get one more try then flip `needs_reconnect` |
| Idempotency | Refresh service is lock-guarded and re-checks freshness |
| Retry | None beyond service-internal transient retry; next hour re-sweeps |
| Failure | `invalid_grant` handling in service (04-security §2); sweep logs summary `{refreshed, failed, flagged}` |

## J5 · CleanupAssetsJob — storage hygiene

| | |
|---|---|
| Trigger | Recurring cron `0 3 * * *` (daily 03:00 UTC) |
| Payload | none |
| Flow | (a) `upload_state='pending'` older than 24 h → delete storage object + row. (b) Soft-deleted assets with **zero** `template_assets`/`email_send_attachments` references → delete object, hard-delete row. (c) Expired `oauth_states`, `password_reset_tokens`, `idempotency_keys`, `user_sessions` purge. (d) Snapshots older than retention (180 d) deleted |
| Idempotency | Deletes are naturally idempotent; storage delete tolerates 404 |
| Failure | Per-item try/catch; run continues; summary log + metric `cleanup.failures` |

## J6 · GeneratePlainTextJob

Plain text is generated **inline** in the render pipeline (08-rendering §2.8) — a separate
job exists only for backfill/regeneration: trigger = manual/one-off enqueue with
`{ templateVersionId }`; writes `text_body` if NULL. Idempotent by definition (skip if set).

## J7 · ProviderSyncJob (post-MVP, optional)

| | |
|---|---|
| Trigger | Recurring `*/15 * * * *`, feature-flagged per user (extra consent adds read scope) |
| Payload | `{ connectedAccountId }` fan-out |
| Flow | Graph: find Sent Items with our `X-MailTemplateHub-Ref` header → backfill `provider_message_id` for `sendMail`-path recipients. Gmail: `messages.get` on stored ids → detect bounces via labels where possible |
| Risk callout | Requires broader scopes (`Mail.Read` / `gmail.readonly` or metadata) — explicitly out of MVP consent; ship only with separate opt-in flow |

## Scheduling wiring

```csharp
RecurringJob.AddOrUpdate<PromoteScheduledSendsJob>("promote-scheduled", j => j.RunAsync(CancellationToken.None), "* * * * *");
RecurringJob.AddOrUpdate<RefreshTokensJob>("refresh-tokens",  j => j.RunAsync(CancellationToken.None), "0 * * * *");
RecurringJob.AddOrUpdate<CleanupAssetsJob>("cleanup",         j => j.RunAsync(CancellationToken.None), "0 3 * * *");
```

`IBackgroundJobScheduler` (Application port) exposes `EnqueueSend(jobId)`,
`ScheduleSendRetry(jobId, delay)` so Application code never references Hangfire.

## Observability & alerts

| Metric | Alert when |
|---|---|
| `send.recipient.duration` (histogram, by provider) | p95 > 10 s sustained |
| `send.recipient.failures` (counter, by kind) | permanent-failure rate > 5% / 15 min |
| `tokens.refresh_failed` | > 0 for same account 3× / day |
| Hangfire queue depth `sends` | > 100 for 10 min |
| J3 tick misses | 5 consecutive |
