"use client";

import { listAccounts, startConnect } from "@/lib/api/accounts";
import { listInbox } from "@/lib/api/inbox";
import { formatMailTime } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { EmptyState } from "@/components/app/empty-state";
import { PageHeader } from "@/components/app/page-header";
import { StampAvatar } from "@/components/app/stamp-avatar";
import { MessageReader } from "@/components/inbox/message-reader";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Inbox, MailOpen, Search } from "lucide-react";
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

  const unread = messages.data?.items.filter((m) => !m.isRead).length ?? 0;
  const mailbox = readable.find((a) => a.id === effectiveAccount);
  const eyebrow = mailbox
    ? [mailbox.emailAddress, messages.data && `${unread} unread`].filter(Boolean).join(" · ")
    : undefined;

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        icon={Inbox}
        eyebrow={eyebrow}
        title="Inbox"
        description="Read what people send you and answer with a template, in the same thread."
      />

      {accounts.data && readable.length === 0 && (
        <EmptyState
          icon={Inbox}
          title={needsGrant.length > 0 ? "Allow reading your mail" : "Connect an account first"}
          description={
            needsGrant.length > 0
              ? "Sending and reading are separate permissions. Reconnect to grant read access; nothing else changes."
              : "Connect Gmail or Outlook to read your inbox and reply with templates."
          }
          action={
            <div className="flex flex-wrap justify-center gap-2">
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
                <Button asChild variant="gradient">
                  <Link href="/accounts">Connect account</Link>
                </Button>
              )}
            </div>
          }
        />
      )}

      {readable.length > 0 && (
        <Card className="gap-0 overflow-hidden p-0 lg:grid lg:h-[calc(100vh-16rem)] lg:min-h-[34rem] lg:grid-cols-[minmax(0,22rem)_1fr]">
          <section
            aria-label="Messages"
            className="flex min-h-0 flex-col border-b-2 lg:border-r-2 lg:border-b-0 dark:border-b dark:lg:border-r dark:lg:border-b-0"
          >
            <div className="space-y-2.5 border-b-2 p-3 dark:border-b">
              {readable.length > 1 && (
                <NativeSelect
                  value={effectiveAccount}
                  onChange={(e) => {
                    setAccountId(e.target.value);
                    setSelectedId(null);
                  }}
                  aria-label="Mailbox"
                >
                  {readable.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.emailAddress}
                    </option>
                  ))}
                </NativeSelect>
              )}
              <form
                role="search"
                className="flex gap-2"
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
                  aria-label="Search this mailbox"
                />
                <Button type="submit" variant="outline" size="icon" aria-label="Search">
                  <Search />
                </Button>
              </form>
            </div>

            <div className="max-h-[60vh] min-h-0 flex-1 overflow-auto lg:max-h-none">
              {messages.isLoading && <p className="p-4 text-sm text-muted-foreground">Loading messages…</p>}
              {messages.isError && <p className="p-4 text-sm text-destructive">Could not load this mailbox.</p>}
              {messages.data?.items.length === 0 && (
                <p className="p-4 text-sm text-muted-foreground">
                  {appliedSearch ? "No messages match your search." : "No messages found."}
                </p>
              )}
              <ul>
                {messages.data?.items.map((m) => {
                  const sender = m.fromName ?? m.fromEmail;
                  const selected = selectedId === m.id;
                  return (
                    <li key={m.id}>
                      <button
                        type="button"
                        onClick={() => setSelectedId(m.id)}
                        aria-current={selected ? "true" : undefined}
                        className={`flex w-full gap-2.5 border-b-2 px-3.5 py-3 text-left transition-colors dark:border-b dark:border-border/60 ${
                          selected
                            ? "bg-primary shadow-[inset_4px_0_0_0_#111111] dark:bg-accent dark:shadow-[inset_3px_0_0_0_var(--primary)]"
                            : "hover:bg-accent"
                        }`}
                      >
                        <StampAvatar name={sender} />
                        <span className="min-w-0 flex-1">
                          <span className="flex items-center justify-between gap-2">
                            <span className={`truncate text-[13px] ${m.isRead ? "text-muted-foreground" : "font-bold"}`}>
                              {sender}
                            </span>
                            <span className="flex shrink-0 items-center gap-1.5 font-mono text-[11px] text-muted-foreground">
                              {!m.isRead && (
                                <span className="size-1.5 rounded-full bg-[#ff7a45] dark:bg-primary" aria-label="Unread" />
                              )}
                              {formatMailTime(m.receivedAt)}
                            </span>
                          </span>
                          <span
                            className={`block truncate text-[13px] ${m.isRead ? "text-muted-foreground" : "font-semibold"}`}
                          >
                            {m.subject || "(no subject)"}
                          </span>
                          <span className="block truncate text-xs text-muted-foreground">{m.snippet}</span>
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ul>
            </div>
          </section>

          <section aria-label="Message" className="flex min-h-0 flex-col">
            {selectedId ? (
              <MessageReader
                key={`${effectiveAccount}:${selectedId}`}
                accountId={effectiveAccount}
                messageId={selectedId}
              />
            ) : (
              <div className="flex flex-1 items-center justify-center p-8">
                <EmptyState
                  icon={MailOpen}
                  title="Pick a message"
                  description="Open a conversation to read it and answer with one of your templates."
                  className="w-full max-w-md border-0 dark:border-0"
                />
              </div>
            )}
          </section>
        </Card>
      )}
    </div>
  );
}
