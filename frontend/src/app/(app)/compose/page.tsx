"use client";

import { listAccounts } from "@/lib/api/accounts";
import { createSend, getTrackingStatus, type RecipientInput } from "@/lib/api/sends";
import { getTemplate, listTemplates } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import { PageHeader } from "@/components/app/page-header";
import { Button } from "@/components/ui/button";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
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
  const [trackClicks, setTrackClicks] = useState(false);
  const [utmCampaign, setUtmCampaign] = useState("");

  const accounts = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const tracking = useQuery({ queryKey: ["tracking-status"], queryFn: getTrackingStatus, staleTime: Infinity });
  const canTrack = tracking.data === true;
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
        trackClicks: canTrack && trackClicks,
        utmCampaign: utmCampaign.trim() || null,
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
            <NativeSelect
              id="account"
              value={effectiveAccount}
              onChange={(e) => setAccountId(e.target.value)}
            >
              {activeAccounts.map((a) => (
                <option key={a.id} value={a.id}>{a.emailAddress} ({a.provider})</option>
              ))}
            </NativeSelect>
          </div>

          <div className="space-y-2">
            <Label htmlFor="template">Template</Label>
            <NativeSelect
              id="template"
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
            >
              <option value="">Select a template…</option>
              {templates.data?.items.map((t) => (
                <option key={t.id} value={t.id}>{t.name}</option>
              ))}
            </NativeSelect>
          </div>

          <div className="space-y-2">
            <Label htmlFor="recipients">Recipients</Label>
            <Textarea
              id="recipients"
              value={recipientsText}
              onChange={(e) => setRecipientsText(e.target.value)}
              placeholder="Comma, semicolon, or newline separated email addresses"
              rows={3}
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

          <fieldset className="space-y-3 rounded-xl border-2 border-dashed p-3.5 dark:border">
            <legend className="px-1 font-mono text-[11px] font-bold tracking-[0.06em] uppercase dark:font-sans dark:font-medium dark:text-muted-foreground">
              Measure
            </legend>
            <label className={`flex items-start gap-2 text-sm ${canTrack ? "" : "opacity-60"}`}>
              <input
                type="checkbox"
                className="mt-0.5"
                checked={canTrack && trackClicks}
                disabled={!canTrack}
                onChange={(e) => setTrackClicks(e.target.checked)}
              />
              <span>
                Track clicks
                <span className="block text-xs text-muted-foreground">
                  {canTrack
                    ? "Links go through a short redirect so you can see who clicked. Unsubscribe links are never tracked."
                    : "Needs the app on a public address recipients can reach. Set Tracking:PublicBaseUrl when you deploy."}
                </span>
              </span>
            </label>
            <div className="space-y-1.5">
              <Label htmlFor="utm-campaign" className="text-xs">UTM campaign (optional)</Label>
              <Input
                id="utm-campaign"
                value={utmCampaign}
                maxLength={100}
                placeholder="spring-launch"
                onChange={(e) => setUtmCampaign(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Tags links with utm_source, utm_medium and utm_campaign, so your site&apos;s analytics shows what this email brought in.
              </p>
            </div>
          </fieldset>

          <Button onClick={() => send.mutate()} disabled={!canSend || send.isPending} className="w-full">
            {send.isPending ? "Sending…" : schedule ? "Schedule send" : "Send now"}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
