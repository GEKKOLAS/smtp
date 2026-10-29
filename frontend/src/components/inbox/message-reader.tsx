"use client";

import { getInboxMessage } from "@/lib/api/inbox";
import { queryKeys } from "@/lib/query/query-keys";
import { StampAvatar } from "@/components/app/stamp-avatar";
import { ReplyWithTemplateDialog } from "@/components/inbox/reply-with-template-dialog";
import { Button } from "@/components/ui/button";
import { useQuery } from "@tanstack/react-query";
import { CornerUpLeft } from "lucide-react";
import { useState } from "react";

/** Renders one message and offers the template reply. */
export function MessageReader({ accountId, messageId }: { accountId: string; messageId: string }) {
  const [replyOpen, setReplyOpen] = useState(false);

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.inboxMessage(accountId, messageId),
    queryFn: () => getInboxMessage(accountId, messageId),
  });

  if (isLoading) return <p className="p-6 text-sm text-muted-foreground">Loading message…</p>;
  if (isError || !data) return <p className="p-6 text-sm text-destructive">Could not load this message.</p>;

  const sender = data.fromName ?? data.fromEmail;

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="space-y-4 border-b-2 p-5 dark:border-b">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <h2 className="min-w-0 flex-1 font-heading text-2xl leading-tight font-bold tracking-[-0.025em] dark:text-lg dark:font-semibold dark:tracking-tight">
            {data.subject || "(no subject)"}
          </h2>
          <Button variant="gradient" className="shrink-0" onClick={() => setReplyOpen(true)}>
            <CornerUpLeft />
            Reply with template
          </Button>
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
              {new Date(data.receivedAt).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}
            </p>
          )}
        </div>
      </header>

      <div className="flex min-h-0 flex-1 flex-col bg-muted/60 p-4">
        {data.html ? (
          // Sender-controlled markup: sanitized server-side and framed with a
          // fully locked sandbox (no same-origin, no scripts) on top of that.
          <iframe
            title={`Message: ${data.subject}`}
            sandbox=""
            srcDoc={data.html}
            className="min-h-[24rem] w-full flex-1 rounded-lg border-2 bg-white dark:border"
          />
        ) : (
          <pre className="flex-1 overflow-auto rounded-lg border-2 bg-card p-4 font-sans text-sm whitespace-pre-wrap dark:border">
            {data.text ?? "(no content)"}
          </pre>
        )}
      </div>

      <ReplyWithTemplateDialog
        accountId={accountId}
        message={data}
        open={replyOpen}
        onOpenChange={setReplyOpen}
      />
    </div>
  );
}
