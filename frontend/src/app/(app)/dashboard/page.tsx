"use client";

import { listAccounts } from "@/lib/api/accounts";
import { listSends } from "@/lib/api/sends";
import { useSession } from "@/lib/hooks/use-session";
import { queryKeys } from "@/lib/query/query-keys";
import { SendStatusBadge } from "@/components/sends/send-status-badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/format";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";

const CHECKLIST = [
  {
    title: "Connect an account",
    description: "Link Gmail or Outlook so you can send from your own address.",
    href: "/accounts",
    cta: "Connect account",
  },
  {
    title: "Create a template",
    description: "Design a reusable email visually or in MJML.",
    href: "/templates",
    cta: "New template",
  },
  {
    title: "Send a test",
    description: "Preview and send a test email to yourself.",
    href: "/compose",
    cta: "Compose",
  },
];

export default function DashboardPage() {
  const { data: user } = useSession();
  const { data: accounts } = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const { data: sends } = useQuery({ queryKey: queryKeys.sends(), queryFn: () => listSends() });

  const needsReconnect = accounts?.filter((a) => a.state === "needs_reconnect") ?? [];
  const recentSends = sends?.items.slice(0, 5) ?? [];
  const hasSetup = (accounts?.length ?? 0) > 0;

  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">
          Welcome{user ? `, ${user.displayName.split(" ")[0]}` : ""}
        </h1>
        <p className="text-muted-foreground">Get set up in three steps.</p>
      </header>

      {needsReconnect.length > 0 && (
        <Card className="border-destructive/40">
          <CardHeader>
            <CardTitle className="text-base text-destructive">Action needed</CardTitle>
            <CardDescription>
              {needsReconnect.length === 1
                ? "One connected account needs to be reconnected before it can send."
                : `${needsReconnect.length} connected accounts need to be reconnected.`}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild variant="outline" size="sm">
              <Link href="/accounts">Review accounts</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      {!hasSetup && (
        <div className="grid gap-4 sm:grid-cols-3">
          {CHECKLIST.map((step, index) => (
            <Card key={step.href} className="flex flex-col">
              <CardHeader className="flex-1">
                <div className="mb-2 flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
                  {index + 1}
                </div>
                <CardTitle className="text-base">{step.title}</CardTitle>
                <CardDescription>{step.description}</CardDescription>
              </CardHeader>
              <CardContent>
                <Button asChild variant="outline" size="sm" className="w-full">
                  <Link href={step.href}>{step.cta}</Link>
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-base">Recent sends</CardTitle>
          {recentSends.length > 0 && (
            <Link href="/sends" className="text-xs text-muted-foreground hover:underline">View all</Link>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {recentSends.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Nothing sent yet.{" "}
              <Link href="/compose" className="font-medium text-foreground hover:underline">Compose an email</Link>.
            </p>
          ) : (
            <ul className="divide-y">
              {recentSends.map((job) => (
                <li key={job.id}>
                  <Link
                    href={`/sends/${job.id}`}
                    className="flex items-center justify-between gap-4 px-6 py-3 hover:bg-muted/40"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{job.subjectSnapshot}</p>
                      <p className="text-xs text-muted-foreground">
                        {job.recipientCounts.sent} sent · {formatDate(job.createdAt)}
                      </p>
                    </div>
                    <SendStatusBadge status={job.status} />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
