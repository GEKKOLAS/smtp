"use client";

import { formatRate, getSendAnalytics } from "@/lib/api/analytics";
import { cancelSend, getSend, retrySend } from "@/lib/api/sends";
import { formatDate } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { isActiveStatus, SendStatusBadge } from "@/components/sends/send-status-badge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CornerUpLeft } from "lucide-react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { toast } from "sonner";

export default function SendDetailPage() {
  const id = useParams().id as string;
  const queryClient = useQueryClient();

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.send(id),
    queryFn: () => getSend(id),
    refetchInterval: (query) =>
      query.state.data && isActiveStatus(query.state.data.job.status) ? 3000 : false,
  });

  const analytics = useQuery({
    queryKey: queryKeys.sendAnalytics(id),
    queryFn: () => getSendAnalytics(id),
    enabled: data !== undefined && !data.job.isTest && !isActiveStatus(data.job.status),
  });
  const clicked = new Set(analytics.data?.recipients.filter((r) => r.clicked).map((r) => r.recipientId));

  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.send(id) });

  const cancel = useMutation({
    mutationFn: () => cancelSend(id),
    onSuccess: () => { invalidate(); toast.success("Send cancelled"); },
    onError: () => toast.error("Could not cancel"),
  });
  const retry = useMutation({
    mutationFn: () => retrySend(id),
    onSuccess: () => { invalidate(); toast.success("Retrying failed recipients"); },
    onError: () => toast.error("Could not retry"),
  });

  if (isLoading) return <p className="p-8 text-sm text-muted-foreground">Loading…</p>;
  if (isError || !data) return <p className="p-8 text-sm text-destructive">Could not load this send.</p>;

  const { job, recipients, events } = data;
  const canCancel = ["scheduled", "queued", "sending", "retrying"].includes(job.status);
  const canRetry = ["failed", "partiallyfailed"].includes(job.status);

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Link href="/sends" className="inline-block text-sm font-semibold text-link hover:underline">
        ← Send history
      </Link>

      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <p className="mb-1.5 font-mono text-xs font-bold tracking-[0.04em] uppercase dark:font-medium dark:text-muted-foreground">
            {[
              job.isTest && "Test send",
              job.isReply && "Reply",
              job.scheduledAt ? `Scheduled for ${formatDate(job.scheduledAt)}` : `Created ${formatDate(job.createdAt)}`,
            ]
              .filter(Boolean)
              .join(" · ")}
          </p>
          <h1 className="font-heading text-3xl leading-tight font-bold tracking-[-0.03em] dark:text-2xl dark:font-semibold dark:tracking-tight">
            {job.subjectSnapshot}
          </h1>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {job.isReply && <CornerUpLeft className="size-4 text-muted-foreground" aria-hidden />}
          <SendStatusBadge status={job.status} />
          {canCancel && (
            <Button variant="outline" size="sm" onClick={() => cancel.mutate()} disabled={cancel.isPending}>
              Cancel
            </Button>
          )}
          {canRetry && (
            <Button size="sm" onClick={() => retry.mutate()} disabled={retry.isPending}>
              Retry failed
            </Button>
          )}
        </div>
      </header>

      {analytics.data && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Results</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <dl className="grid grid-cols-3 gap-3 text-sm">
              <div>
                <dt className="text-xs text-muted-foreground">Accepted</dt>
                <dd className="font-mono text-lg font-semibold tabular-nums">{analytics.data.totals.accepted}</dd>
              </div>
              <div>
                <dt className="text-xs text-muted-foreground">Bounced</dt>
                <dd className="font-mono text-lg font-semibold tabular-nums">{analytics.data.totals.bounced}</dd>
              </div>
              <div>
                <dt className="text-xs text-muted-foreground">Click rate</dt>
                <dd className="font-mono text-lg font-semibold tabular-nums">
                  {analytics.data.trackClicks ? formatRate(analytics.data.totals.clickRate) : "Not tracked"}
                </dd>
              </div>
            </dl>
            {analytics.data.utmCampaign && (
              <p className="text-xs text-muted-foreground">
                UTM campaign: <span className="font-mono">{analytics.data.utmCampaign}</span>
              </p>
            )}
            {analytics.data.links.length > 0 && (
              <ul className="space-y-1.5 border-t-2 pt-3 text-sm dark:border-t">
                {analytics.data.links.map((link) => (
                  <li key={link.position} className="flex items-baseline gap-3">
                    <span className="min-w-0 flex-1 truncate" title={link.url}>
                      {link.url.replace(/^https?:\/\//, "")}
                    </span>
                    <span className="shrink-0 font-mono text-xs text-muted-foreground tabular-nums">
                      {link.uniqueClickers} people · {link.clicks} clicks
                      {link.scannerClicks > 0 && ` · ${link.scannerClicks} scanner`}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      )}

      <Card className="gap-0 pb-0">
        <CardHeader className="pb-3"><CardTitle className="text-base">Recipients · {recipients.length}</CardTitle></CardHeader>
        <CardContent className="p-0">
          <ul className="divide-y-2 border-t-2 dark:divide-y dark:border-t dark:divide-border/60">
            {recipients.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-4 px-4 py-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{r.email}</p>
                  {r.failureMessage && <p className="text-xs text-destructive">{r.failureMessage}</p>}
                  {r.status === "bounced" && (
                    <p className="text-xs text-warning">
                      Bounced{r.bounceCode && ` (${r.bounceCode})`}
                      {r.bounceReason && `: ${r.bounceReason}`}
                    </p>
                  )}
                  {r.providerMessageId && (
                    <p className="truncate font-mono text-[11px] text-muted-foreground">id: {r.providerMessageId}</p>
                  )}
                </div>
                <div className="flex shrink-0 items-center gap-1.5">
                  {clicked.has(r.id) && <Badge variant="info">Clicked</Badge>}
                  <Badge
                    variant={
                      r.status === "sent"
                        ? "success"
                        : r.status === "failed"
                          ? "destructive"
                          : r.status === "bounced"
                            ? "warning"
                            : "secondary"
                    }
                  >
                    {r.status}
                  </Badge>
                </div>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      {events.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-base">Provider events</CardTitle></CardHeader>
          <CardContent>
            <ul className="space-y-1 font-mono text-xs text-muted-foreground">
              {events.map((e, i) => (
                <li key={i}>
                  {formatDate(e.createdAt)} · {e.eventType}
                  {e.providerErrorCode && ` (${e.providerErrorCode})`}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
