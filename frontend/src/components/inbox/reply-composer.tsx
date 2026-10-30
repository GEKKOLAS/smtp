"use client";

import { ApiError } from "@/lib/api/client";
import { createSend, getSend, listSends } from "@/lib/api/sends";
import { getTemplate, listTemplates } from "@/lib/api/templates";
import { queryKeys } from "@/lib/query/query-keys";
import type { InboxMessage } from "@/lib/schemas/inbox";
import type { SendJob } from "@/lib/schemas/sends";
import { isActiveStatus } from "@/components/sends/send-status-badge";
import { SendTimeline } from "@/components/sends/send-timeline";
import { TemplatePreviewThumb } from "@/components/templates/template-preview-thumb";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Loader2, Send, X } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

/** What a reply needs to know about the message it answers; a list item or the full message both fit. */
export type ReplyTarget = Pick<InboxMessage, "id" | "fromEmail" | "fromName" | "subject"> & {
  snippet?: string;
  replySubject?: string;
};

const firstNameOf = (target: ReplyTarget) => target.fromName?.trim().split(/\s+/)[0] || target.fromEmail;

/** Fills the obvious variables from the sender so a phone reply is usually just "pick and send". */
function suggestedValue(variable: string, target: ReplyTarget): string | undefined {
  const key = variable.toLowerCase().replace(/[^a-z]/g, "");
  if (["firstname", "first", "name"].includes(key)) return target.fromName ? firstNameOf(target) : undefined;
  if (["fullname", "recipientname", "contactname"].includes(key)) return target.fromName ?? undefined;
  if (["email", "recipientemail", "contactemail"].includes(key)) return target.fromEmail;
  return undefined;
}

/**
 * Answers an inbox message with a template: full screen on phones, a dialog on
 * larger screens. Only the provider message id is sent: the backend resolves
 * the recipient and the thread headers from the mailbox, so the reply lands in
 * the original conversation. After sending it shows the live send status.
 */
export function ReplyComposer({
  accountId,
  accountEmail,
  message,
  onClose,
  onDone = onClose,
}: {
  accountId: string;
  accountEmail?: string;
  message: ReplyTarget;
  onClose: () => void;
  /** "Back to letters" after sending; defaults to just closing. */
  onDone?: () => void;
}) {
  const queryClient = useQueryClient();
  const [templateId, setTemplateId] = useState("");
  const [subject, setSubject] = useState("");
  const [variables, setVariables] = useState<Record<string, string>>({});
  const [sent, setSent] = useState<SendJob | null>(null);

  const templates = useQuery({
    queryKey: queryKeys.templates(),
    queryFn: () => listTemplates({ pageSize: 100 }),
  });
  const sendable = templates.data?.items.filter((t) => t.currentVersionId) ?? [];
  const selected = sendable.find((t) => t.id === templateId);

  const template = useQuery({
    queryKey: queryKeys.template(templateId),
    queryFn: () => getTemplate(templateId),
    enabled: templateId.length > 0,
  });
  const templateVariables = template.data?.currentVersion?.variablesSchema ?? [];

  const defaultSubject = message.replySubject ?? (/^re:/i.test(message.subject) ? message.subject : `Re: ${message.subject}`);
  const valueOf = (name: string) => variables[name] ?? suggestedValue(name, message) ?? "";

  const send = useMutation({
    mutationFn: () =>
      createSend({
        connectedEmailAccountId: accountId,
        templateVersionId: selected!.currentVersionId!,
        variables: Object.fromEntries(
          templateVariables.map((v) => [v.name, valueOf(v.name)] as const).filter(([, value]) => value !== ""),
        ),
        replyToMessageId: message.id,
        // Null lets the backend derive "Re: …" from the original message.
        subjectOverride: subject.trim() || null,
      }),
    onSuccess: (job) => {
      setSent(job);
      void queryClient.invalidateQueries({ queryKey: ["sends"] });
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.errorCode : "";
      toast.error(
        code === "send.variables_missing"
          ? "Fill in the template variables before replying."
          : code === "inbox.scope_missing"
            ? "Reconnect this account to allow reading mail."
            : code === "send.too_large"
              ? "The message exceeds the size limit."
              : "Could not send the reply.",
      );
    },
  });

  const canSend = !!selected && !send.isPending;
  const sender = message.fromName ? `${message.fromName} (${message.fromEmail})` : message.fromEmail;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        showCloseButton={false}
        className="flex flex-col gap-0 overflow-hidden p-0 sm:max-w-xl max-sm:top-0 max-sm:left-0 max-sm:h-dvh max-sm:max-h-none max-sm:max-w-none max-sm:translate-x-0 max-sm:translate-y-0 max-sm:rounded-none max-sm:shadow-none max-sm:ring-0"
      >
        {sent ? (
          <ReplySent initial={sent} recipient={firstNameOf(message)} onDone={onDone} />
        ) : (
          <>
            <div className="flex h-14 shrink-0 items-center justify-between gap-2 border-b-2 px-2 pt-[env(safe-area-inset-top)] dark:border-b">
              <Button variant="ghost" onClick={onClose} className="h-9 text-[15px] font-semibold text-muted-foreground">
                Cancel
              </Button>
              <DialogTitle className="min-w-0 truncate text-[15px] font-bold dark:font-semibold">
                Reply to {firstNameOf(message)}
              </DialogTitle>
              <Button
                variant="ghost"
                onClick={() => send.mutate()}
                disabled={!canSend}
                className="h-9 text-[15px] font-semibold text-link"
              >
                Send
              </Button>
            </div>

            <div className="min-h-0 flex-1 space-y-5 overflow-y-auto px-4 py-4 sm:px-5">
              <DialogDescription className="sr-only">
                Answer {sender} with one of your templates, in the same conversation.
              </DialogDescription>

              <div className="rounded-xl border-2 bg-muted/60 px-3.5 py-2.5 dark:border">
                <p className="truncate font-mono text-[11px] font-bold tracking-wide text-muted-foreground uppercase dark:font-medium">
                  {message.subject || "(no subject)"}
                </p>
                {message.snippet && <p className="mt-1 line-clamp-2 text-sm">“{message.snippet}”</p>}
                <p className="mt-1 truncate text-xs text-muted-foreground">from {sender}</p>
              </div>

              <section className="space-y-2">
                <div className="flex items-baseline justify-between gap-3">
                  <h3 className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
                    Pick a template
                  </h3>
                  {templates.data && <span className="font-mono text-xs text-muted-foreground">{sendable.length} ready</span>}
                </div>
                {templates.isLoading && (
                  <div className="flex gap-3 pt-2 pb-3" aria-label="Loading templates">
                    {[0, 1, 2].map((i) => (
                      <div key={i} className="aspect-[3/4] w-36 shrink-0 animate-pulse rounded-xl bg-muted" />
                    ))}
                  </div>
                )}
                {templates.isError && <p className="text-sm text-destructive">Could not load your templates.</p>}
                {templates.data && sendable.length === 0 && (
                  <p className="text-sm text-muted-foreground">
                    No templates with content yet.{" "}
                    <Link href="/templates" className="font-semibold text-link hover:underline">
                      Create one
                    </Link>{" "}
                    on a larger screen first.
                  </p>
                )}
                {sendable.length > 0 && (
                  <div
                    role="radiogroup"
                    aria-label="Template"
                    className="-mx-4 flex snap-x snap-mandatory scroll-px-4 gap-3 overflow-x-auto px-4 pt-2 pb-3 [scrollbar-width:none] sm:-mx-5 sm:scroll-px-5 sm:px-5"
                  >
                    {sendable.map((t) => {
                      const on = t.id === templateId;
                      return (
                        <button
                          key={t.id}
                          type="button"
                          role="radio"
                          aria-checked={on}
                          onClick={() => setTemplateId(t.id)}
                          className="w-36 shrink-0 snap-start text-left"
                        >
                          <span
                            className={`relative block overflow-hidden rounded-xl border-2 shadow-hard-sm dark:border dark:shadow-none ${
                              on ? "outline-3 outline-offset-3 outline-foreground dark:outline-primary" : ""
                            }`}
                          >
                            <TemplatePreviewThumb versionId={t.currentVersionId} className="aspect-[3/4]" />
                            {on && (
                              <span className="absolute top-2 right-2 flex size-6 items-center justify-center rounded-full border-2 bg-primary text-primary-foreground dark:border-0">
                                <Check className="size-3.5" strokeWidth={3} />
                              </span>
                            )}
                          </span>
                          <span className="mt-2 block truncate text-[13px] font-semibold">{t.name}</span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </section>

              {selected && (
                <section className="space-y-2">
                  <h3 className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
                    Personalize
                  </h3>
                  <div className="overflow-hidden rounded-xl border-2 bg-card dark:border">
                    <label className="flex items-center gap-3 px-3.5 py-2.5 focus-within:bg-accent">
                      <span className="w-28 shrink-0 text-sm font-semibold">Subject</span>
                      <input
                        value={subject}
                        onChange={(e) => setSubject(e.target.value)}
                        placeholder={defaultSubject}
                        className="min-w-0 flex-1 bg-transparent text-right text-sm outline-none placeholder:text-muted-foreground"
                      />
                    </label>
                    {template.isLoading && (
                      <p className="border-t-2 px-3.5 py-2.5 text-sm text-muted-foreground dark:border-t">
                        Loading variables…
                      </p>
                    )}
                    {templateVariables.map((v) => (
                      <label
                        key={v.name}
                        className="flex items-center gap-3 border-t-2 px-3.5 py-2.5 focus-within:bg-accent dark:border-t"
                      >
                        <span className="w-28 shrink-0 truncate font-mono text-[13px] font-bold dark:font-medium">
                          {`{{${v.name}}}`}
                        </span>
                        <input
                          value={valueOf(v.name)}
                          onChange={(e) => setVariables((prev) => ({ ...prev, [v.name]: e.target.value }))}
                          placeholder={v.sample ?? v.default ?? (v.required ? "Required" : "Optional")}
                          inputMode={v.type === "url" ? "url" : undefined}
                          className="min-w-0 flex-1 bg-transparent text-right text-sm outline-none placeholder:text-muted-foreground"
                        />
                      </label>
                    ))}
                  </div>
                </section>
              )}

              <p className="font-mono text-xs text-muted-foreground">
                Goes back in the same thread{accountEmail && ` · from ${accountEmail}`}
              </p>
            </div>

            <div className="shrink-0 border-t-2 px-4 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))] sm:px-5 dark:border-t">
              <Button variant="gradient" onClick={() => send.mutate()} disabled={!canSend} className="h-12 w-full text-base">
                {send.isPending ? <Loader2 className="animate-spin" /> : <Send />}
                {send.isPending ? "Sending…" : selected ? "Send reply" : "Pick a template to reply"}
              </Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** The "it went out" moment: a stamp that follows the job's live status, then today's activity. */
function ReplySent({ initial, recipient, onDone }: { initial: SendJob; recipient: string; onDone: () => void }) {
  const live = useQuery({
    queryKey: queryKeys.send(initial.id),
    queryFn: () => getSend(initial.id),
    refetchInterval: (query) => (isActiveStatus(query.state.data?.job.status ?? initial.status) ? 2000 : false),
  });
  const job = live.data?.job ?? initial;

  const recent = useQuery({ queryKey: queryKeys.sends(), queryFn: () => listSends() });
  const today = new Date().toDateString();
  const todays = [job, ...(recent.data?.items ?? []).filter((j) => j.id !== job.id)].filter(
    (j) => new Date(j.createdAt).toDateString() === today,
  );

  const failed = job.status === "failed" || job.status === "partiallyfailed";
  const copy =
    job.status === "sent"
      ? { stamp: "Sent", icon: Check, title: "Reply delivered", body: `${recipient} will see it in the same conversation.` }
      : failed
        ? { stamp: "Failed", icon: X, title: "The reply didn't go out", body: "Open the details to see why and retry." }
        : job.status === "cancelled"
          ? { stamp: "Stopped", icon: X, title: "Reply cancelled", body: "Nothing was sent." }
          : { stamp: "Sending", icon: Loader2, title: "Sending your reply…", body: "This usually takes a few seconds." };

  return (
    <>
      <div className="min-h-0 flex-1 space-y-6 overflow-y-auto px-4 pt-[max(2.5rem,env(safe-area-inset-top))] pb-4 sm:px-5">
        <div
          role="status"
          className={`mx-auto flex size-[132px] -rotate-3 flex-col items-center justify-center gap-0.5 rounded-[20px] font-mono text-lg font-bold tracking-[0.08em] uppercase outline-2 outline-offset-[-10px] outline-[#111111] outline-dashed dark:rotate-0 dark:rounded-full dark:outline-0 ${
            failed
              ? "bg-[#ff7a45] text-[#111111] dark:bg-destructive/15 dark:text-destructive"
              : "bg-primary text-primary-foreground dark:bg-primary/14 dark:text-link"
          }`}
        >
          <copy.icon className={`size-8 ${copy.icon === Loader2 ? "animate-spin" : ""}`} strokeWidth={2.75} />
          {copy.stamp}
        </div>
        <div className="text-center">
          <DialogTitle className="font-heading text-[2rem] leading-tight font-bold tracking-[-0.035em] dark:text-2xl dark:font-semibold dark:tracking-tight">
            {copy.title}
          </DialogTitle>
          <DialogDescription className="mt-2 text-base">{copy.body}</DialogDescription>
        </div>
        {todays.length > 0 && (
          <section className="space-y-2">
            <div className="flex items-baseline justify-between">
              <h3 className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">Today</h3>
              <span className="font-mono text-xs text-muted-foreground">
                {todays.length} {todays.length === 1 ? "send" : "sends"}
              </span>
            </div>
            <SendTimeline jobs={todays.slice(0, 5)} />
          </section>
        )}
      </div>
      <div className="flex shrink-0 gap-2 border-t-2 px-4 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))] sm:px-5 dark:border-t">
        <Button variant="outline" onClick={onDone} className="h-12 flex-1 text-base">
          Back to letters
        </Button>
        <Button asChild variant="ghost" className="h-12 text-base">
          <Link href={`/sends/${job.id}`}>Details</Link>
        </Button>
      </div>
    </>
  );
}
