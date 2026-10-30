"use client";

import { listAccounts } from "@/lib/api/accounts";
import { listSends } from "@/lib/api/sends";
import { queryKeys } from "@/lib/query/query-keys";
import { EmptyState } from "@/components/app/empty-state";
import { PageHeader } from "@/components/app/page-header";
import { isActiveStatus } from "@/components/sends/send-status-badge";
import { SendsTable } from "@/components/sends/sends-table";
import { groupByDay, SendTimeline } from "@/components/sends/send-timeline";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useQuery } from "@tanstack/react-query";
import { History } from "lucide-react";
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

  const { data: accounts } = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.sends(status),
    queryFn: () => listSends({ status }),
    // Poll while any listed job is still active.
    refetchInterval: (query) =>
      query.state.data?.items.some((j) => isActiveStatus(j.status)) ? 4000 : false,
  });

  const accountEmail = new Map(accounts?.map((a) => [a.id, a.emailAddress]));
  const inProgress = data?.items.filter((j) => isActiveStatus(j.status)).length ?? 0;
  const eyebrow = data
    ? [`${data.totalCount} ${data.totalCount === 1 ? "send" : "sends"}`, inProgress > 0 && `${inProgress} in progress`]
        .filter(Boolean)
        .join(" · ")
    : undefined;

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <PageHeader
        icon={History}
        eyebrow={eyebrow}
        title="Send history"
        description="Every send and its per-recipient status."
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          {FILTERS.map((f) => (
            <TabsTrigger key={f.value} value={f.value}>
              {f.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">Could not load send history.</p>}

      {data && data.items.length === 0 && (
        <EmptyState
          icon={History}
          title={tab === "all" ? "No sends yet" : `Nothing ${FILTERS.find((f) => f.value === tab)?.label.toLowerCase()}`}
          action={
            tab === "all" ? (
              <Button asChild variant="gradient">
                <Link href="/compose">Compose your first email</Link>
              </Button>
            ) : undefined
          }
        />
      )}

      {data && data.items.length > 0 && (
        <>
          <Card className="gap-0 py-0 max-lg:hidden [&_th]:border-t-0">
            <SendsTable jobs={data.items} accountEmail={accountEmail} />
          </Card>
          <div className="space-y-5 lg:hidden">
            {groupByDay(data.items).map((group) => (
              <section key={group.label} className="space-y-2">
                <div className="flex items-baseline justify-between">
                  <h2 className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
                    {group.label}
                  </h2>
                  <span className="font-mono text-xs text-muted-foreground">
                    {group.jobs.length} {group.jobs.length === 1 ? "send" : "sends"}
                  </span>
                </div>
                <SendTimeline jobs={group.jobs} />
              </section>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
