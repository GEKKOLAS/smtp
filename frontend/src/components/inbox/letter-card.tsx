"use client";

import { formatMailTime } from "@/lib/format";
import type { InboxMessage } from "@/lib/schemas/inbox";
import { initialsOf, toneFor } from "@/components/app/stamp-avatar";
import { CornerUpLeft } from "lucide-react";
import { useRef, useState } from "react";

const ACTION_WIDTH = 76;
const MOVE_THRESHOLD = 6;

/**
 * One inbox message as a letter on the phone screen. Tap opens it; swipe left
 * reveals Reply. `revealed` is owned by the list so only one letter is open at a time.
 */
export function LetterCard({
  message,
  revealed,
  onReveal,
  onOpen,
  onReply,
}: {
  message: InboxMessage;
  revealed: boolean;
  onReveal: (revealed: boolean) => void;
  onOpen: () => void;
  onReply: () => void;
}) {
  const sender = message.fromName ?? message.fromEmail;
  const [drag, setDrag] = useState<number | null>(null);
  const gesture = useRef<{ x: number; base: number; moved: boolean } | null>(null);
  const suppressClick = useRef(false);

  const offset = drag ?? (revealed ? -ACTION_WIDTH : 0);

  return (
    <div className="relative">
      <div className="absolute inset-y-1 right-0 flex" aria-hidden={!revealed}>
        <button
          type="button"
          tabIndex={revealed ? 0 : -1}
          onClick={() => {
            onReveal(false);
            onReply();
          }}
          style={{ width: ACTION_WIDTH - 8 }}
          className="flex flex-col items-center justify-center gap-1 rounded-2xl border-2 bg-primary text-[11px] font-bold text-primary-foreground dark:border-0"
        >
          <CornerUpLeft className="size-[18px]" strokeWidth={2.25} />
          Reply
        </button>
      </div>

      <button
        type="button"
        onPointerDown={(e) => {
          gesture.current = { x: e.clientX, base: revealed ? -ACTION_WIDTH : 0, moved: false };
        }}
        onPointerMove={(e) => {
          const g = gesture.current;
          if (!g) return;
          const delta = e.clientX - g.x;
          if (!g.moved && Math.abs(delta) < MOVE_THRESHOLD) return;
          if (!g.moved) {
            g.moved = true;
            e.currentTarget.setPointerCapture(e.pointerId);
          }
          setDrag(Math.min(0, Math.max(-ACTION_WIDTH * 1.25, g.base + delta)));
        }}
        onPointerUp={(e) => {
          const g = gesture.current;
          gesture.current = null;
          setDrag(null);
          if (!g?.moved) return;
          suppressClick.current = true;
          onReveal(g.base + (e.clientX - g.x) < -ACTION_WIDTH / 2);
        }}
        onPointerCancel={() => {
          gesture.current = null;
          setDrag(null);
        }}
        onClick={() => {
          if (suppressClick.current) {
            suppressClick.current = false;
            return;
          }
          if (revealed) onReveal(false);
          else onOpen();
        }}
        style={{ transform: `translateX(${offset}px)` }}
        className={`relative block w-full touch-pan-y rounded-2xl border-2 bg-card py-3.5 pr-16 pl-4 text-left select-none dark:border ${
          drag === null ? "transition-transform duration-200" : ""
        } ${message.isRead ? "" : "shadow-hard-sm dark:shadow-none"}`}
      >
        <span className="flex items-baseline gap-2">
          <span className={`min-w-0 truncate text-base ${message.isRead ? "font-medium" : "font-bold"}`}>{sender}</span>
          <span className="shrink-0 font-mono text-[11px] text-muted-foreground">{formatMailTime(message.receivedAt)}</span>
        </span>
        <span
          className={`mt-0.5 block truncate text-sm ${message.isRead ? "text-muted-foreground" : "font-semibold"}`}
        >
          {message.subject || "(no subject)"}
        </span>
        {message.snippet && (
          <span className="mt-1 block truncate font-mono text-xs text-muted-foreground">{message.snippet}</span>
        )}
        <span
          aria-hidden
          className={`absolute top-3 right-3 flex h-[46px] w-10 rotate-[2.5deg] items-center justify-center rounded-[3px] border-2 border-dashed border-[#111111] text-[13px] font-bold text-[#111111] dark:rotate-0 dark:rounded-xl dark:border-0 ${toneFor(sender)}`}
        >
          {initialsOf(sender)}
        </span>
        {!message.isRead && <span className="sr-only">Unread</span>}
      </button>
    </div>
  );
}
