"use client";

import { auditLabel, listAuditLogs } from "@/lib/api/audit";
import { EmptyState } from "@/components/app/empty-state";
import { PageHeader } from "@/components/app/page-header";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { useQuery } from "@tanstack/react-query";
import { ShieldCheck } from "lucide-react";

function formatWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

export default function AuditPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["audit-logs"],
    queryFn: () => listAuditLogs(),
  });

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        icon={ShieldCheck}
        title="Security & activity"
        description="A record of security-relevant events on your account."
      />

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">Could not load your activity.</p>}

      {data && data.items.length === 0 && (
        <EmptyState icon={ShieldCheck} title="No activity yet" description="Sign-ins, password changes and API keys will be recorded here." />
      )}

      {data && data.items.length > 0 && (
        <Card className="py-0">
          <CardContent className="p-0">
            <ul className="divide-y-2 dark:divide-y dark:divide-border/60">
              {data.items.map((log) => (
                <li key={log.id} className="flex items-center justify-between gap-4 px-4 py-3">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold">{auditLabel(log.action)}</span>
                      {log.action.startsWith("auth.login_failed") && (
                        <Badge variant="destructive">Failed</Badge>
                      )}
                    </div>
                    <p className="font-mono text-[11px] text-muted-foreground">
                      {log.ip ?? "unknown IP"} · {formatWhen(log.createdAt)}
                    </p>
                  </div>
                  <code className="shrink-0 rounded-md bg-muted px-1.5 py-0.5 font-mono text-[11px] text-muted-foreground">{log.action}</code>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
