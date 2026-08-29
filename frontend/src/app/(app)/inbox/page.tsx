"use client";

import { listAccounts, startConnect } from "@/lib/api/accounts";
import { listInbox } from "@/lib/api/inbox";
import { queryKeys } from "@/lib/query/query-keys";
import { PageHeader } from "@/components/app/page-header";
import { MessageReader } from "@/components/inbox/message-reader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Inbox } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

export default function InboxPage() {
  const [accountId, setAccountId] = useState("");
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const accounts = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });

  const readable = accounts.data?.filter((a) => a.canReadInbox) ?? [];
  const needsGrant = accounts.data?.filter((a) => a.state === "active" && !a.canReadInbox) ?? [];
  const effectiveAccount = accountId || readable[0]?.id || "";

  const messages = useQuery({
    queryKey: queryKeys.inbox(effectiveAccount, appliedSearch),
    queryFn: () => listInbox({ accountId: effectiveAccount, search: appliedSearch || undefined }),
    enabled: effectiveAccount.length > 0,
  });

  const grantRead = useMutation({
    mutationFn: (provider: "gmail" | "outlook") => startConnect(provider, "/inbox"),
    onSuccess: (url) => window.location.assign(url),
    onError: () => toast.error("Could not start the reconnect"),
  });

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        icon={Inbox}
        title="Inbox"
        description="Read what people send you and answer them with a template, in the same thread."
      />

      {accounts.data && readable.length === 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {needsGrant.length > 0 ? "Allow reading your mail" : "Connect an account first"}
            </CardTitle>
            <CardDescription>
              {needsGrant.length > 0
                ? "Sending and reading are separate permissions. Reconnect to grant read access; nothing else changes."
                : "Connect Gmail or Outlook to read your inbox and reply with templates."}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {needsGrant.length > 0 ? (
              needsGrant.map((account) => (
                <Button
                  key={account.id}
                  variant="outline"
                  onClick={() => grantRead.mutate(account.provider)}
                  disabled={grantRead.isPending}
                >
                  Reconnect {account.emailAddress}
                </Button>
              ))
            ) : (
              <Button asChild variant="outline">
                <Link href="/accounts">Connect account</Link>
              </Button>
            )}
          </CardContent>
        </Card>
      )}

      {readable.length > 0 && (
        <>
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={effectiveAccount}
              onChange={(e) => {
                setAccountId(e.target.value);
                setSelectedId(null);
              }}
              aria-label="Mailbox"
              className="rounded-md border bg-background px-3 py-2 text-sm"
            >
              {readable.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.emailAddress}
                </option>
              ))}
            </select>

            <form
              className="flex flex-1 gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                setAppliedSearch(search.trim());
                setSelectedId(null);
              }}
            >
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search this mailbox…"
                className="max-w-sm"
              />
              <Button type="submit" variant="outline">
                Search
              </Button>
            </form>
          </div>

          <div className="grid gap-4 lg:grid-cols-[minmax(0,20rem)_1fr]">
            <Card className="overflow-hidden p-0">
              <div className="max-h-[70vh] overflow-auto">
                {messages.isLoading && (
                  <p className="p-4 text-sm text-muted-foreground">Loading messages…</p>
                )}
                {messages.isError && (
                  <p className="p-4 text-sm text-destructive">Could not load this mailbox.</p>
                )}
                {messages.data?.items.length === 0 && (
                  <p className="p-4 text-sm text-muted-foreground">No messages found.</p>
                )}
                <ul className="divide-y">
                  {messages.data?.items.map((m) => (
                    <li key={m.id}>
                      <button
                        type="button"
                        onClick={() => setSelectedId(m.id)}
                        aria-current={selectedId === m.id ? "true" : undefined}
                        className={`w-full px-4 py-3 text-left transition-colors hover:bg-muted/60 ${
                          selectedId === m.id ? "bg-muted" : ""
                        }`}
                      >
                        <p
                          className={`truncate text-sm ${m.isRead ? "text-muted-foreground" : "font-semibold"}`}
                        >
                          {m.fromName ?? m.fromEmail}
                        </p>
                        <p className="truncate text-sm">{m.subject}</p>
                        <p className="truncate text-xs text-muted-foreground">{m.snippet}</p>
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            </Card>

            <Card className="overflow-hidden p-0">
              {selectedId ? (
                <MessageReader
                  key={`${effectiveAccount}:${selectedId}`}
                  accountId={effectiveAccount}
                  messageId={selectedId}
                />
              ) : (
                <p className="p-6 text-sm text-muted-foreground">
                  Select a message to read it and reply with a template.
                </p>
              )}
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
