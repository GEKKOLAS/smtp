"use client";

import { auditLabel, listAuditLogs } from "@/lib/api/audit";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { useQuery } from "@tanstack/react-query";

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
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Security &amp; activity</h1>
        <p className="text-muted-foreground">A record of security-relevant events on your account.</p>
      </header>

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">Could not load your activity.</p>}

      {data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed py-16 text-center">
          <p className="text-sm text-muted-foreground">Security events will appear here.</p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <Card>
          <CardContent className="p-0">
            <ul className="divide-y">
              {data.items.map((log) => (
                <li key={log.id} className="flex items-center justify-between gap-4 px-6 py-3">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{auditLabel(log.action)}</span>
                      {log.action.startsWith("auth.login_failed") && (
                        <Badge variant="destructive">Failed</Badge>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {log.ip ?? "unknown IP"} · {formatWhen(log.createdAt)}
                    </p>
                  </div>
                  <code className="shrink-0 text-xs text-muted-foreground">{log.action}</code>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
