"use client";

import { listAccounts } from "@/lib/api/accounts";
import { createSend, type RecipientInput } from "@/lib/api/sends";
import { getTemplate, listTemplates } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import { PageHeader } from "@/components/app/page-header";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useMutation, useQuery } from "@tanstack/react-query";
import { PenSquare } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useMemo, useState } from "react";
import { toast } from "sonner";

export default function ComposePage() {
  return (
    <Suspense fallback={null}>
      <ComposeInner />
    </Suspense>
  );
}

function ComposeInner() {
  const router = useRouter();
  const params = useSearchParams();
  const [accountId, setAccountId] = useState("");
  const [templateId, setTemplateId] = useState(params.get("templateId") ?? "");
  const [recipientsText, setRecipientsText] = useState("");
  const [variables, setVariables] = useState<Record<string, string>>({});
  const [schedule, setSchedule] = useState(false);
  const [scheduledAt, setScheduledAt] = useState("");

  const accounts = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const templates = useQuery({
    queryKey: queryKeys.templates(),
    queryFn: () => listTemplates({ pageSize: 100 }),
  });
  const template = useQuery({
    queryKey: queryKeys.template(templateId),
    queryFn: () => getTemplate(templateId),
    enabled: templateId.length > 0,
  });

  const activeAccounts = accounts.data?.filter((a) => a.state === "active") ?? [];
  const effectiveAccount = accountId || activeAccounts[0]?.id || "";
  const templateVariables = template.data?.currentVersion?.variablesSchema ?? [];

  const recipients: RecipientInput[] = useMemo(
    () =>
      recipientsText
        .split(/[\n,;]+/)
        .map((e) => e.trim())
        .filter((e) => e.length > 0)
        .map((email) => ({ email })),
    [recipientsText],
  );

  const send = useMutation({
    mutationFn: () =>
      createSend({
        connectedEmailAccountId: effectiveAccount,
        templateVersionId: template.data!.currentVersion!.id,
        recipients,
        variables,
        scheduledAt: schedule && scheduledAt ? new Date(scheduledAt).toISOString() : null,
      }),
    onSuccess: (job) => {
      toast.success(schedule ? "Send scheduled" : "Send queued");
      router.push(`/sends/${job.id}`);
    },
    onError: (e) => {
      const code = e instanceof ApiError ? e.errorCode : "";
      const message =
        code === "send.variables_missing"
          ? "Some recipients are missing required variables."
          : code === "send.too_large"
            ? "The message exceeds the size limit."
            : e instanceof ApiError
              ? e.message ?? "Send failed"
              : "Send failed";
      toast.error(message);
    },
  });

  const canSend =
    effectiveAccount.length > 0 &&
    template.data?.currentVersion != null &&
    recipients.length > 0 &&
    (!schedule || scheduledAt.length > 0);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader icon={PenSquare} title="Compose" description="Send a template to one or more recipients." />

      {accounts.data && activeAccounts.length === 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Connect an account first</CardTitle>
            <CardDescription>You need an active Gmail or Outlook connection to send.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild variant="outline"><Link href="/accounts">Connect account</Link></Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="space-y-4 py-5">
          <div className="space-y-2">
            <Label htmlFor="account">From account</Label>
            <select
              id="account"
              value={effectiveAccount}
              onChange={(e) => setAccountId(e.target.value)}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm"
            >
              {activeAccounts.map((a) => (
                <option key={a.id} value={a.id}>{a.emailAddress} ({a.provider})</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="template">Template</Label>
            <select
              id="template"
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm"
            >
              <option value="">Select a template…</option>
              {templates.data?.items.map((t) => (
                <option key={t.id} value={t.id}>{t.name}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="recipients">Recipients</Label>
            <textarea
              id="recipients"
              value={recipientsText}
              onChange={(e) => setRecipientsText(e.target.value)}
              placeholder="Comma, semicolon, or newline separated email addresses"
              rows={3}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm"
            />
            <p className="text-xs text-muted-foreground">{recipients.length} recipient(s), 50 max</p>
          </div>

          {templateVariables.length > 0 && (
            <div className="space-y-2">
              <Label>Variables</Label>
              {templateVariables.map((v) => (
                <div key={v.name} className="flex items-center gap-2">
                  <span className="w-32 shrink-0 font-mono text-xs">{`{{${v.name}}}`}</span>
                  <Input
                    value={variables[v.name] ?? ""}
                    placeholder={v.sample ?? v.default ?? ""}
                    onChange={(e) => setVariables((prev) => ({ ...prev, [v.name]: e.target.value }))}
                    className="h-8 text-sm"
                  />
                </div>
              ))}
              <p className="text-xs text-muted-foreground">Applied to all recipients.</p>
            </div>
          )}

          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={schedule} onChange={(e) => setSchedule(e.target.checked)} />
              Schedule for later
            </label>
            {schedule && (
              <Input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} />
            )}
          </div>

          <Button onClick={() => send.mutate()} disabled={!canSend || send.isPending} className="w-full">
            {send.isPending ? "Sending…" : schedule ? "Schedule send" : "Send now"}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
