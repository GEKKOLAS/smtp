"use client";

import { StampAvatar } from "@/components/app/stamp-avatar";
import { Sparkles } from "lucide-react";

// Roughly what Gmail's desktop list shows of subject + preheader before truncating.
const VISIBLE_CHARS = 90;

/**
 * Subject and preheader edited in place, laid out like the row recipients will see in their
 * inbox — so the preheader reads as the continuation of the subject, the way it actually appears.
 */
export function InboxPreviewBar({
  senderName,
  subject,
  preheader,
  onSubjectChange,
  onPreheaderChange,
  onRewrite,
}: {
  senderName: string;
  subject: string;
  preheader: string;
  onSubjectChange: (value: string) => void;
  onPreheaderChange: (value: string) => void;
  /** Present when the AI copy assistant is available. */
  onRewrite?: (field: "subject" | "preheader") => void;
}) {
  const visible = subject.length + preheader.length;

  const rewriteButton = (field: "subject" | "preheader", value: string) =>
    onRewrite && (
      <button
        type="button"
        onClick={() => onRewrite(field)}
        disabled={value.trim().length === 0}
        aria-label={`Rewrite ${field} with AI`}
        title={`Rewrite ${field} with AI`}
        className="flex size-6 shrink-0 items-center justify-center self-center rounded-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground disabled:pointer-events-none disabled:opacity-40"
      >
        <Sparkles className="size-3.5" />
      </button>
    );

  return (
    <div className="shrink-0 border-b-2 bg-background px-4 py-3 dark:border-b">
      <div className="mx-auto flex max-w-5xl items-center gap-3 rounded-xl border-2 bg-card px-3.5 py-2.5 shadow-hard-sm dark:border dark:shadow-none">
        <StampAvatar name={senderName} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-3">
            <span className="truncate text-[13px] font-bold">{senderName}</span>
            <span
              className={`shrink-0 font-mono text-[11px] ${visible > VISIBLE_CHARS ? "text-warning" : "text-muted-foreground"}`}
              title="Subject + preheader characters. Inbox lists usually cut off around 90."
            >
              Inbox preview · {visible}/{VISIBLE_CHARS}
            </span>
          </div>
          <div className="flex min-w-0 items-baseline gap-1.5 text-[13px]">
            <input
              aria-label="Subject"
              value={subject}
              onChange={(e) => onSubjectChange(e.target.value)}
              placeholder="Subject line"
              className="field-sizing-content max-w-[60%] min-w-28 shrink-0 rounded-sm bg-transparent font-semibold outline-none placeholder:font-normal placeholder:text-muted-foreground hover:bg-accent focus:bg-accent"
            />
            {rewriteButton("subject", subject)}
            <span aria-hidden className="shrink-0 text-muted-foreground">
              –
            </span>
            <input
              aria-label="Preheader"
              value={preheader}
              onChange={(e) => onPreheaderChange(e.target.value)}
              placeholder="Preheader: the preview text shown after the subject"
              className="min-w-0 flex-1 rounded-sm bg-transparent text-muted-foreground outline-none placeholder:text-muted-foreground/70 hover:bg-accent focus:bg-accent"
            />
            {rewriteButton("preheader", preheader)}
          </div>
        </div>
      </div>
    </div>
  );
}
