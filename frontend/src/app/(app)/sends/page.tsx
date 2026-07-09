"use client";

import { listSends } from "@/lib/api/sends";
import { formatDate } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { SendStatusBadge, isActiveStatus } from "@/components/sends/send-status-badge";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";

const FILTERS = [
  { value: "all", label: "All", status: undefined },
  { value: "scheduled", label: "Scheduled", status: "scheduled" },
  { value: "sent", label: "Sent", status: "sent" },
  { value: "failed", label: "Failed", status: "failed" },
];

export default function SendsPage() {
  const [tab, setTab] = useState("all");
  const status = FILTERS.find((f) => f.value === tab)?.status;

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.sends(status),
    queryFn: () => listSends({ status }),
    // Poll while any listed job is still active.
    refetchInterval: (query) =>
      query.state.data?.items.some((j) => isActiveStatus(j.status)) ? 4000 : false,
  });

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Send history</h1>
        <p className="text-muted-foreground">Every send and its per-recipient status.</p>
      </header>

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          {FILTERS.map((f) => (
            <TabsTrigger key={f.value} value={f.value}>{f.label}</TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">Could not load send history.</p>}

      {data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed py-16 text-center">
          <p className="mb-4 text-sm text-muted-foreground">No sends yet.</p>
          <Link href="/compose" className="text-sm font-medium hover:underline">Compose your first email</Link>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="space-y-2">
          {data.items.map((job) => {
            const c = job.recipientCounts;
            const total = c.pending + c.sending + c.sent + c.failed + c.cancelled;
            return (
              <li key={job.id}>
                <Link href={`/sends/${job.id}`}>
                  <Card className="transition-colors hover:border-foreground/30">
                    <CardContent className="flex items-center justify-between gap-4 py-3">
                      <div className="min-w-0">
                        <p className="truncate font-medium">
                          {job.isTest && <span className="mr-1 text-xs text-muted-foreground">[TEST]</span>}
                          {job.subjectSnapshot}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {c.sent}/{total} sent
                          {c.failed > 0 && ` · ${c.failed} failed`}
                          {" · "}
                          {job.scheduledAt ? `scheduled ${formatDate(job.scheduledAt)}` : formatDate(job.createdAt)}
                        </p>
                      </div>
                      <SendStatusBadge status={job.status} />
                    </CardContent>
                  </Card>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
