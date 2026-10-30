"use client";

import { listAccounts, startConnect } from "@/lib/api/accounts";
import { listInbox } from "@/lib/api/inbox";
import { formatMailTime } from "@/lib/format";
import { useIsDesktop } from "@/lib/hooks/use-media-query";
import { queryKeys } from "@/lib/query/query-keys";
import type { InboxMessage } from "@/lib/schemas/inbox";
import { EmptyState } from "@/components/app/empty-state";
import { PageHeader } from "@/components/app/page-header";
import { StampAvatar } from "@/components/app/stamp-avatar";
import { LetterCard } from "@/components/inbox/letter-card";
import { MessageReader } from "@/components/inbox/message-reader";
import { ReplyComposer } from "@/components/inbox/reply-composer";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Inbox, MailOpen, Search, X } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

const CHIP =
  "inline-flex h-[34px] shrink-0 items-center gap-1.5 rounded-full border-2 px-3.5 text-[13px] font-semibold whitespace-nowrap transition-colors dark:border";
const CHIP_ON = "border-foreground bg-primary text-primary-foreground dark:border-transparent";
const CHIP_OFF = "bg-card text-foreground";

export default function InboxPage() {
  const isDesktop = useIsDesktop();
  const [accountId, setAccountId] = useState("");
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Phone-only state: the unread filter, the search field, the swiped letter and a reply started from a swipe.
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [swipedId, setSwipedId] = useState<string | null>(null);
  const [replyTo, setReplyTo] = useState<InboxMessage | null>(null);

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

  const emptyState = accounts.data && readable.length === 0 && (
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
  );

  const mailboxSelect = readable.length > 1 && (
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
  );

  const searchForm = (
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
        autoFocus={!isDesktop}
      />
      <Button type="submit" variant="outline" size="icon" aria-label="Search">
        <Search />
      </Button>
    </form>
  );

  if (!isDesktop) {
    if (selectedId) {
      return (
        <MessageReader
          key={`${effectiveAccount}:${selectedId}`}
          accountId={effectiveAccount}
          accountEmail={mailbox?.emailAddress}
          messageId={selectedId}
          onBack={() => setSelectedId(null)}
        />
      );
    }

    const items = messages.data?.items ?? [];
    const waiting = items.filter((m) => !m.isRead);
    const earlier = unreadOnly ? [] : items.filter((m) => m.isRead);
    const letter = (m: InboxMessage) => (
      <li key={m.id}>
        <LetterCard
          message={m}
          revealed={swipedId === m.id}
          onReveal={(open) => setSwipedId(open ? m.id : null)}
          onOpen={() => setSelectedId(m.id)}
          onReply={() => setReplyTo(m)}
        />
      </li>
    );

    return (
      <div className="space-y-4">
        <header>
          <p className="font-mono text-xs font-bold tracking-[0.05em] uppercase dark:font-medium dark:text-muted-foreground">
            {new Date().toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" })}
          </p>
          <h1 className="mt-1.5 font-heading text-[2.375rem] leading-[1.05] font-bold tracking-[-0.04em] dark:text-3xl dark:font-semibold dark:tracking-tight">
            {!messages.data
              ? "Letters"
              : unread > 0
                ? `${unread} ${unread === 1 ? "letter" : "letters"} waiting`
                : "All caught up"}
          </h1>
        </header>

        {emptyState}

        {readable.length > 0 && (
          <>
            {mailboxSelect}
            <div className="-mx-4 flex gap-2 overflow-x-auto px-4 [scrollbar-width:none]">
              <button
                type="button"
                aria-pressed={!unreadOnly}
                onClick={() => setUnreadOnly(false)}
                className={`${CHIP} ${unreadOnly ? CHIP_OFF : CHIP_ON}`}
              >
                All
              </button>
              <button
                type="button"
                aria-pressed={unreadOnly}
                onClick={() => setUnreadOnly(true)}
                className={`${CHIP} ${unreadOnly ? CHIP_ON : CHIP_OFF}`}
              >
                Unread · {unread}
              </button>
              <button
                type="button"
                aria-expanded={searchOpen}
                onClick={() => setSearchOpen((open) => !open)}
                className={`${CHIP} ${appliedSearch ? CHIP_ON : CHIP_OFF}`}
              >
                <Search className="size-3.5" />
                {appliedSearch ? `“${appliedSearch}”` : "Search"}
              </button>
              {appliedSearch && (
                <button
                  type="button"
                  aria-label="Clear search"
                  onClick={() => {
                    setSearch("");
                    setAppliedSearch("");
                    setSearchOpen(false);
                  }}
                  className={`${CHIP} ${CHIP_OFF} px-2.5`}
                >
                  <X className="size-3.5" />
                </button>
              )}
            </div>
            {searchOpen && searchForm}

            {messages.isLoading && (
              <ul className="space-y-3" aria-label="Loading letters">
                {[0, 1, 2].map((i) => (
                  <li key={i} className="h-[86px] animate-pulse rounded-2xl bg-card/70" />
                ))}
              </ul>
            )}
            {messages.isError && <p className="text-sm text-destructive">Could not load this mailbox.</p>}
            {messages.data && waiting.length + earlier.length === 0 && (
              <EmptyState
                icon={MailOpen}
                title={appliedSearch ? "No letters match" : unreadOnly ? "Nothing unread" : "No letters yet"}
                description={unreadOnly && !appliedSearch ? "You've answered everything. Nice." : undefined}
              />
            )}

            {waiting.length > 0 && <ul className="space-y-3">{waiting.map(letter)}</ul>}
            {earlier.length > 0 && (
              <section className="space-y-3">
                {waiting.length > 0 && (
                  <h2 className="pt-1 font-mono text-xs tracking-[0.05em] text-muted-foreground uppercase">Earlier</h2>
                )}
                <ul className="space-y-3">{earlier.map(letter)}</ul>
              </section>
            )}
            {waiting.length > 0 && !swipedId && (
              <p className="text-center font-mono text-[11px] text-muted-foreground">Swipe a letter left to reply</p>
            )}
          </>
        )}

        {replyTo && (
          <ReplyComposer
            key={replyTo.id}
            accountId={effectiveAccount}
            accountEmail={mailbox?.emailAddress}
            message={replyTo}
            onClose={() => setReplyTo(null)}
          />
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        icon={Inbox}
        eyebrow={eyebrow}
        title="Inbox"
        description="Read what people send you and answer with a template, in the same thread."
      />

      {emptyState}

      {readable.length > 0 && (
        <Card className="gap-0 overflow-hidden p-0 lg:grid lg:h-[calc(100vh-16rem)] lg:min-h-[34rem] lg:grid-cols-[minmax(0,22rem)_1fr]">
          <section
            aria-label="Messages"
            className="flex min-h-0 flex-col border-b-2 lg:border-r-2 lg:border-b-0 dark:border-b dark:lg:border-r dark:lg:border-b-0"
          >
            <div className="space-y-2.5 border-b-2 p-3 dark:border-b">
              {mailboxSelect}
              {searchForm}
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
                accountEmail={mailbox?.emailAddress}
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
