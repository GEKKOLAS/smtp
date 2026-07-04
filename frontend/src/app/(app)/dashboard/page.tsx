"use client";

import { listAccounts } from "@/lib/api/accounts";
import { useSession } from "@/lib/hooks/use-session";
import { queryKeys } from "@/lib/query/query-keys";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
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

  const needsReconnect = accounts?.filter((a) => a.state === "needs_reconnect") ?? [];

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

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Recent activity</CardTitle>
          <CardDescription>Your sends and scheduled emails will appear here.</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="py-8 text-center text-sm text-muted-foreground">
            Nothing sent yet. Connect an account to get started.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
