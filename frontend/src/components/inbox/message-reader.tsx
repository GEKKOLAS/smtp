"use client";

import { getInboxMessage } from "@/lib/api/inbox";
import { formatMailTime } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { StampAvatar } from "@/components/app/stamp-avatar";
import { ReplyComposer } from "@/components/inbox/reply-composer";
import { Button } from "@/components/ui/button";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, CornerUpLeft } from "lucide-react";
import { useState } from "react";

/**
 * Renders one message and offers the template reply. Pass `onBack` for the
 * full-screen phone layout: it adds a back bar and returns there after replying.
 */
export function MessageReader({
  accountId,
  accountEmail,
  messageId,
  onBack,
}: {
  accountId: string;
  accountEmail?: string;
  messageId: string;
  onBack?: () => void;
}) {
  const [replyOpen, setReplyOpen] = useState(false);

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.inboxMessage(accountId, messageId),
    queryFn: () => getInboxMessage(accountId, messageId),
  });

  const backBar = onBack && (
    <div className="flex items-center justify-between gap-2 pb-3">
      <Button variant="ghost" onClick={onBack} className="-ml-2.5 h-9 gap-0.5 text-[15px] font-semibold">
        <ChevronLeft className="size-5" />
        Letters
      </Button>
      {data && (
        <Button variant="gradient" onClick={() => setReplyOpen(true)} className="h-9">
          <CornerUpLeft />
          Reply
        </Button>
      )}
    </div>
  );

  if (isLoading || isError || !data) {
    return (
      <div>
        {backBar}
        {isLoading ? (
          <p className="p-6 text-sm text-muted-foreground">Loading message…</p>
        ) : (
          <p className="p-6 text-sm text-destructive">Could not load this message.</p>
        )}
      </div>
    );
  }

  const sender = data.fromName ?? data.fromEmail;

  return (
    <div className="flex h-full min-h-0 flex-col">
      {backBar}
      <header className={onBack ? "space-y-4 pb-4" : "space-y-4 border-b-2 p-5 dark:border-b"}>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <h2 className="min-w-0 flex-1 font-heading text-2xl leading-tight font-bold tracking-[-0.025em] dark:text-lg dark:font-semibold dark:tracking-tight">
            {data.subject || "(no subject)"}
          </h2>
          {!onBack && (
            <Button variant="gradient" className="shrink-0" onClick={() => setReplyOpen(true)}>
              <CornerUpLeft />
              Reply with template
            </Button>
          )}
        </div>
        <div className="flex items-center gap-3">
          <StampAvatar name={sender} className="size-10 text-[13px]" />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm">
              <span className="font-bold dark:font-semibold">{sender}</span>
              {data.fromName && <span className="text-muted-foreground"> &lt;{data.fromEmail}&gt;</span>}
            </p>
            {data.to.length > 0 && <p className="truncate text-xs text-muted-foreground">to {data.to.join(", ")}</p>}
          </div>
          {data.receivedAt && (
            <p className="shrink-0 font-mono text-xs text-muted-foreground">
              {onBack
                ? formatMailTime(data.receivedAt)
                : new Date(data.receivedAt).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}
            </p>
          )}
        </div>
      </header>

      <div className={`flex min-h-0 flex-1 flex-col bg-muted/60 ${onBack ? "-mx-4 p-2" : "p-4"}`}>
        {data.html ? (
          // Sender-controlled markup: sanitized server-side and framed with a
          // fully locked sandbox (no same-origin, no scripts) on top of that.
          <iframe
            title={`Message: ${data.subject}`}
            sandbox=""
            srcDoc={data.html}
            className={`w-full flex-1 rounded-lg border-2 bg-white dark:border ${onBack ? "min-h-[70dvh]" : "min-h-[24rem]"}`}
          />
        ) : (
          <pre className="flex-1 overflow-auto rounded-lg border-2 bg-card p-4 font-sans text-sm whitespace-pre-wrap dark:border">
            {data.text ?? "(no content)"}
          </pre>
        )}
      </div>

      {replyOpen && (
        <ReplyComposer
          accountId={accountId}
          accountEmail={accountEmail}
          message={data}
          onClose={() => setReplyOpen(false)}
          onDone={onBack}
        />
      )}
    </div>
  );
}
