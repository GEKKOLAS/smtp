"use client";

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

      <Card className="gap-0 pb-0">
        <CardHeader className="pb-3"><CardTitle className="text-base">Recipients · {recipients.length}</CardTitle></CardHeader>
        <CardContent className="p-0">
          <ul className="divide-y-2 border-t-2 dark:divide-y dark:border-t dark:divide-border/60">
            {recipients.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-4 px-4 py-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{r.email}</p>
                  {r.failureMessage && <p className="text-xs text-destructive">{r.failureMessage}</p>}
                  {r.providerMessageId && (
                    <p className="truncate font-mono text-[11px] text-muted-foreground">id: {r.providerMessageId}</p>
                  )}
                </div>
                <Badge variant={r.status === "sent" ? "success" : r.status === "failed" ? "destructive" : "secondary"}>
                  {r.status}
                </Badge>
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
