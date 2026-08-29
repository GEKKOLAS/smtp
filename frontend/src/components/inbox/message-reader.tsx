"use client";

import { getInboxMessage } from "@/lib/api/inbox";
import { queryKeys } from "@/lib/query/query-keys";
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

  return (
    <div className="flex h-full flex-col">
      <div className="space-y-3 border-b p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <h2 className="truncate text-base font-semibold">{data.subject}</h2>
            <p className="truncate text-sm text-muted-foreground">
              {data.fromName ? `${data.fromName} <${data.fromEmail}>` : data.fromEmail}
            </p>
            {data.receivedAt && (
              <p className="text-xs text-muted-foreground">
                {new Date(data.receivedAt).toLocaleString()}
              </p>
            )}
          </div>
          <Button size="sm" className="shrink-0" onClick={() => setReplyOpen(true)}>
            <CornerUpLeft className="size-3.5" />
            Reply with template
          </Button>
        </div>
      </div>

      <div className="flex-1 overflow-auto bg-muted/30 p-4">
        {data.html ? (
          // Sender-controlled markup: sanitized server-side and framed with a
          // fully locked sandbox (no same-origin, no scripts) on top of that.
          <iframe
            title={`Message: ${data.subject}`}
            sandbox=""
            srcDoc={data.html}
            className="h-[60vh] w-full rounded-md border bg-white"
          />
        ) : (
          <pre className="rounded-md border bg-background p-4 text-sm whitespace-pre-wrap">
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
