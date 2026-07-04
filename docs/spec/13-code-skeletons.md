# 13 — Code Skeletons (key modules)

Illustrative, compile-shaped skeletons establishing the patterns; not exhaustive.

## Backend

### Domain entity + enums

```csharp
// Domain/Entities/EmailSendJob.cs
public sealed class EmailSendJob
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public Guid ConnectedEmailAccountId { get; init; }
    public Guid TemplateVersionId { get; init; }
    public SendJobStatus Status { get; private set; } = SendJobStatus.Queued;
    public bool IsTest { get; init; }
    public string SubjectSnapshot { get; init; } = string.Empty;
    public JsonDocument VariableValues { get; init; } = JsonDocument.Parse("{}");
    public DateTimeOffset? ScheduledAt { get; init; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? FailureCode { get; private set; }
    public string? RenderedSnapshotKey { get; private set; }

    public List<EmailSendRecipient> Recipients { get; init; } = [];
    public List<EmailSendAttachment> Attachments { get; init; } = [];

    // State machine lives on the entity — invalid transitions throw DomainException.
    public void MarkSending(DateTimeOffset now)
    {
        if (Status is not (SendJobStatus.Queued or SendJobStatus.Retrying))
            throw new DomainException(ErrorCodes.Send.InvalidTransition);
        Status = SendJobStatus.Sending;
        StartedAt ??= now;
        AttemptCount++;
    }

    public void Finalize(DateTimeOffset now)
    {
        var (sent, failed) = (Recipients.Count(r => r.Status == RecipientStatus.Sent),
                              Recipients.Count(r => r.Status == RecipientStatus.Failed));
        Status = (sent, failed) switch
        {
            (> 0, 0) => SendJobStatus.Sent,
            (> 0, > 0) => SendJobStatus.PartiallyFailed,
            _ => SendJobStatus.Failed,
        };
        CompletedAt = now;
    }
}

public enum SendJobStatus { Scheduled, Queued, Sending, Sent, PartiallyFailed, Failed, Retrying, Cancelled }
public enum RecipientStatus { Pending, Sending, Sent, Failed, Cancelled }
public enum EmailProvider { Gmail, Outlook }
```

### Use-case handler + validator (thin-controller pattern)

```csharp
// Application/Features/Sends/Create/CreateSendJob.cs
public sealed record CreateSendJobCommand(
    Guid AccountId, Guid TemplateVersionId,
    IReadOnlyList<RecipientInput> Recipients,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<AttachmentInput> Attachments,
    DateTimeOffset? ScheduledAt, string? IdempotencyKey);

public sealed class CreateSendJobValidator : AbstractValidator<CreateSendJobCommand>
{
    public CreateSendJobValidator(SendLimitsOptions limits)
    {
        RuleFor(x => x.Recipients).NotEmpty().Must(r => r.Count <= limits.MaxRecipients);
        RuleForEach(x => x.Recipients).ChildRules(r =>
            r.RuleFor(x => x.Email).EmailAddress().Must(NoControlChars));
        RuleFor(x => x.ScheduledAt)
            .Must(t => t is null || t > DateTimeOffset.UtcNow.AddMinutes(2))
            .WithErrorCode("send.schedule_too_soon");
    }
}

public sealed class CreateSendJobHandler(
    IAppDbContext db, ICurrentUser user, ITemplateRenderer renderer,
    ISendBudgetCalculator budget, IBackgroundJobScheduler jobs,
    IAuditWriter audit, IClock clock)
{
    public async Task<SendJobDto> HandleAsync(CreateSendJobCommand cmd, CancellationToken ct)
    {
        // Ownership: query filters scope to user.Id; missing ⇒ NotFoundException ⇒ 404.
        var account = await db.ConnectedEmailAccounts
            .FirstOrDefaultAsync(a => a.Id == cmd.AccountId, ct)
            ?? throw new NotFoundException();
        if (account.State != AccountState.Active)
            throw new ConflictException("send.account_needs_reconnect");

        var version = await db.EmailTemplateVersions
            .Include(v => v.TemplateAssets)
            .FirstOrDefaultAsync(v => v.Id == cmd.TemplateVersionId, ct)
            ?? throw new NotFoundException();

        await renderer.ValidateVariablesAsync(version, cmd.Variables, cmd.Recipients, ct); // 422 w/ per-recipient detail
        await budget.EnsureWithinLimitAsync(version, cmd.Attachments, ct);                 // 422 send.too_large

        var job = EmailSendJob.Create(user.Id, account, version, cmd, clock.UtcNow);
        db.EmailSendJobs.Add(job);
        await audit.WriteAsync(AuditAction.SendCreated, job.Id,
            new { recipients = job.Recipients.Count, scheduled = cmd.ScheduledAt is not null }, ct);
        await db.SaveChangesAsync(ct);

        if (job.Status == SendJobStatus.Queued) jobs.EnqueueSend(job.Id);
        return SendJobDto.From(job);
    }
}

// Api/Controllers/SendsController.cs — nothing but transport concerns
[ApiController, Route("api/v1/sends")]
public sealed class SendsController(CreateSendJobHandler create) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("send")]
    [ProducesResponseType<SendJobDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Create(CreateSendJobRequest req, CancellationToken ct)
        => Accepted(await create.HandleAsync(req.ToCommand(Request.Headers.IdempotencyKey), ct));
}
```

### Token cipher (AES-256-GCM envelope)

```csharp
// Infrastructure/Security/AesGcmTokenCipher.cs
public sealed class AesGcmTokenCipher(IOptions<TokenCryptoOptions> opts) : ITokenCipher
{
    public EncryptedToken Encrypt(string plaintext, Guid accountId)
    {
        var dek = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ct = new byte[plaintext.Length]; var tag = new byte[16];
        using var aes = new AesGcm(dek, 16);
        aes.Encrypt(nonce, Encoding.UTF8.GetBytes(plaintext), ct, tag,
                    associatedData: accountId.ToByteArray());     // binds ciphertext to row
        return new EncryptedToken(
            Ciphertext: [.. ct, .. tag], Nonce: nonce,
            WrappedDek: WrapWithKek(dek, opts.Value.ActiveKekVersion),
            KekVersion: opts.Value.ActiveKekVersion);
    }
    public string Decrypt(EncryptedToken token, Guid accountId) { /* inverse, throws CryptographicException on tamper */ }
    private byte[] WrapWithKek(byte[] dek, int version) { /* AesGcm with KEK from options/KMS */ }
}
```

### Gmail provider client (error-mapping shape)

```csharp
// Infrastructure/Providers/Google/GmailEmailProviderClient.cs
public sealed class GmailEmailProviderClient(
    IEmailMessageBuilder mime, ILogger<GmailEmailProviderClient> log) : IEmailProviderClient
{
    public EmailProvider Provider => EmailProvider.Gmail;

    public async Task<ProviderSendResult> SendAsync(
        ConnectedAccountContext account, OutgoingEmail email, CancellationToken ct)
    {
        var built = mime.Build(email);
        using var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = GoogleCredential.FromAccessToken(account.AccessToken),
            ApplicationName = "MailTemplateHub",
        });
        try
        {
            using var stream = built.ToStream();                       // raw RFC 2822
            var request = service.Users.Messages.Send(
                new Message(), "me", stream, "message/rfc822");         // resumable upload
            var progress = await request.UploadAsync(ct);
            if (progress.Exception is not null) throw progress.Exception;
            var sent = request.ResponseBody;
            return new ProviderSendResult(sent.Id, sent.ThreadId, "sent");
        }
        catch (GoogleApiException ex)
        {
            throw new ProviderSendException(
                GoogleErrorMap.Classify(ex),                            // table from 07 §3.1
                retryAfter: GoogleErrorMap.RetryAfter(ex),
                safeMessage: GoogleErrorMap.SafeMessage(ex), inner: ex);
        }
    }
    // GetProfileAsync / RevokeAsync elided
}
```

### Send job (claim loop core)

```csharp
// Infrastructure/Jobs/SendEmailJob.cs
[AutomaticRetry(Attempts = 0)]  // our state machine owns retries (10-jobs.md J1)
public sealed class SendEmailJob(
    IAppDbContext db, ITemplateRenderer renderer, ITokenRefreshService tokens,
    IEmailProviderClientFactory clients, IEmailSendFinalizer finalizer,
    IClock clock, ILogger<SendEmailJob> log)
{
    public async Task RunAsync(Guid sendJobId, CancellationToken ct)
    {
        var job = await db.EmailSendJobs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.Id == sendJobId, ct);
        if (job is null || job.Status is SendJobStatus.Cancelled or SendJobStatus.Sent) return; // idempotent re-entry

        job.MarkSending(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            // Atomic claim — safe with multiple workers.
            var recipient = await db.ClaimNextRecipientAsync(sendJobId, ct); // FOR UPDATE SKIP LOCKED
            if (recipient is null) break;

            using var _ = log.BeginScope(new { SendJobId = sendJobId, RecipientId = recipient.Id });
            try
            {
                var rendered = await renderer.RenderForRecipientAsync(job, recipient, ct); // cached prepared HTML
                var ctx = await tokens.GetValidContextAsync(job.ConnectedEmailAccountId, ct);
                var result = await clients.For(ctx.Provider)
                    .SendAsync(ctx, rendered.ToOutgoingEmail(ctx.FromAddress), ct);
                recipient.MarkSent(result.ProviderMessageId, clock.UtcNow);
            }
            catch (ProviderSendException ex) when (ex.Kind is ProviderErrorKind.Transient or ProviderErrorKind.QuotaExceeded)
            {
                recipient.ScheduleRetry(ex.RetryAfter, clock.UtcNow); // pending + next_attempt_at, or failed when exhausted
            }
            catch (ProviderSendException ex)
            {
                recipient.MarkFailed(ex.Kind, ex.SafeMessage, clock.UtcNow);
                if (ex.Kind == ProviderErrorKind.AuthRevoked)
                { await finalizer.FailRemainingAsync(job, ex, ct); break; }
            }
            await db.SaveChangesAsync(ct);
            await Task.Delay(clients.For(job.Provider).MinSendInterval, ct); // per-account throttle
        }

        await finalizer.FinalizeOrScheduleRetryAsync(job, ct); // snapshot, status, next Hangfire enqueue if retrying
    }
}
```

### Composition root (Program.cs shape)

```csharp
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration)
    .Destructure.With<SecretScrubbingPolicy>());

builder.Services
    .AddApplication()                 // handlers, validators (assembly scan)
    .AddInfrastructure(builder.Configuration)  // DbContext, providers, storage, cipher, Hangfire
    .AddApiServices();                // auth cookie scheme, CSRF, rate limiter policies, ProblemDetails

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                       .AddNpgsql().AddSource("MailTemplateHub.Jobs").AddOtlpExporter());

var app = builder.Build();
app.UseExceptionHandler(); app.UseSerilogRequestLogging();
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.MapControllers();
if (app.Configuration.GetValue<bool>("Jobs:RunInProcess")) app.MapHangfireDashboard("/hangfire", new() { Authorization = [new LocalOnlyAuth()] });
app.Run();
```

## Frontend

### API client wrapper

```ts
// lib/api/client.ts
import { ApiError, problemDetailsSchema } from "@/lib/schemas/errors";

export async function api<T>(path: string, init: RequestInit & { schema?: z.ZodType<T> } = {}): Promise<T> {
  const res = await fetch(`/api/v1${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-Token": getCsrfCookie(),
      ...init.headers,
    },
    credentials: "include",
  });
  if (res.status === 401) { window.location.assign(`/login?next=${encodeURIComponent(location.pathname)}`); throw new ApiError(401, "unauthenticated"); }
  if (!res.ok) {
    const problem = problemDetailsSchema.safeParse(await res.json().catch(() => ({})));
    throw ApiError.fromProblem(res.status, problem.success ? problem.data : undefined);
  }
  if (res.status === 204) return undefined as T;
  const json = await res.json();
  return init.schema ? init.schema.parse(json) : (json as T);
}
```

### Send-job polling hook

```ts
// lib/hooks/useSendJob.ts
const ACTIVE = new Set(["queued", "sending", "retrying"]);

export function useSendJob(id: string) {
  return useQuery({
    queryKey: queryKeys.sends.detail(id),
    queryFn: () => api(`/sends/${id}`, { schema: sendJobDetailSchema }),
    refetchInterval: (q) => (q.state.data && ACTIVE.has(q.state.data.status) ? 3000 : false),
  });
}

export function useCreateSend() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateSendInput) =>
      api("/sends", { method: "POST", body: JSON.stringify(input),
                      headers: { "Idempotency-Key": input.idempotencyKey },
                      schema: sendJobSchema }),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.sends.all }),
  });
}
```

### FilePond presigned upload

```tsx
// components/assets/UploadDropzone.tsx
"use client";
export function UploadDropzone({ onUploaded }: { onUploaded: (a: Asset) => void }) {
  return (
    <FilePond
      allowMultiple
      acceptedFileTypes={ALLOWED_MIME}
      maxFileSize="25MB"
      server={{
        process: async (_field, file, _meta, load, error, progress, abort) => {
          try {
            const grant = await api("/assets/uploads", {
              method: "POST",
              body: JSON.stringify({ filename: file.name, mimeType: file.type, sizeBytes: file.size }),
              schema: uploadGrantSchema,
            });
            await putWithProgress(grant.uploadUrl, file, grant.headers, progress); // XHR PUT straight to MinIO/S3
            const asset = await api(`/assets/uploads/${grant.assetId}/complete`, { method: "POST", schema: assetSchema });
            onUploaded(asset); load(asset.id);
          } catch (e) { error(e instanceof ApiError ? e.message : "Upload failed"); }
          return { abort };
        },
      }}
    />
  );
}
```

### GrapesJS editor (client-only)

```tsx
// components/editor/GrapesEditor.tsx
"use client";
import grapesjs, { type Editor } from "grapesjs";
import mjmlPlugin from "grapesjs-mjml";

export function GrapesEditor({ project, onChange }: {
  project: object | null;
  onChange: (state: { project: object; mjml: string }) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const editorRef = useRef<Editor>(null);

  useEffect(() => {
    const editor = grapesjs.init({
      container: ref.current!,
      plugins: [mjmlPlugin],
      storageManager: false,                      // we own persistence
      assetManager: { custom: true },             // routed to AssetPickerDialog
      blockManager: { blocks: MTH_BLOCKS },       // text/button/image/gif/columns/social/variable-chip
    });
    if (project) editor.loadProjectData(project);
    const emit = debounce(() =>
      onChange({ project: editor.getProjectData(), mjml: editor.getHtml() /* mjml via plugin */ }), 800);
    editor.on("update", emit);
    editorRef.current = editor;
    return () => editor.destroy();
  }, []);

  return <div ref={ref} className="h-full min-h-0" />;
}

// pages import it without SSR:
const GrapesEditor = dynamic(() => import("@/components/editor/GrapesEditor").then(m => m.GrapesEditor), { ssr: false });
```
