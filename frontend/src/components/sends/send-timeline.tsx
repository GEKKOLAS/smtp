import type { SendJob, SendStatus } from "@/lib/schemas/sends";
import { SendStatusBadge } from "@/components/sends/send-status-badge";
import { formatMailTime } from "@/lib/format";
import Link from "next/link";

const DOT: Record<SendStatus, string> = {
  sent: "bg-primary",
  queued: "bg-[#5d9cf2]",
  sending: "bg-[#5d9cf2]",
  retrying: "bg-[#fbbf24]",
  partiallyfailed: "bg-[#fbbf24]",
  failed: "bg-[#ff7a45]",
  scheduled: "bg-card dark:bg-muted-foreground",
  cancelled: "bg-muted-foreground",
};

function summary(job: SendJob) {
  const c = job.recipientCounts;
  const total = c.pending + c.sending + c.sent + c.failed + c.cancelled;
  if (job.isTest) return "test send";
  if (job.isReply) return "reply";
  if (total === 1) return "1 recipient";
  return `${c.sent} of ${total} delivered`;
}

/** Send jobs as a compact activity list; the phone counterpart of `SendsTable`. */
export function SendTimeline({ jobs }: { jobs: SendJob[] }) {
  return (
    <ul className="overflow-hidden rounded-2xl border-2 bg-card shadow-hard-sm dark:border dark:shadow-none">
      {jobs.map((job) => (
        <li key={job.id} className="border-b-2 last:border-b-0 dark:border-b dark:border-border/60">
          <Link href={`/sends/${job.id}`} className="flex items-start gap-3 px-3.5 py-3 transition-colors hover:bg-accent">
            <span
              aria-hidden
              className={`mt-1.5 size-2.5 shrink-0 rounded-full border-2 border-[#111111] dark:border-0 ${DOT[job.status]}`}
            />
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-semibold">{job.subjectSnapshot}</span>
              <span className="mt-0.5 block truncate font-mono text-xs text-muted-foreground">
                {job.scheduledAt && job.status === "scheduled"
                  ? `for ${formatMailTime(job.scheduledAt)}`
                  : formatMailTime(job.createdAt)}{" "}
                · {summary(job)}
              </span>
            </span>
            <SendStatusBadge status={job.status} />
          </Link>
        </li>
      ))}
    </ul>
  );
}

/** Splits jobs (newest first) into Today / Yesterday / dated groups. */
export function groupByDay(jobs: SendJob[]) {
  const today = new Date();
  const yesterday = new Date(today);
  yesterday.setDate(today.getDate() - 1);

  const groups: { label: string; jobs: SendJob[] }[] = [];
  for (const job of jobs) {
    const date = new Date(job.createdAt);
    const label =
      date.toDateString() === today.toDateString()
        ? "Today"
        : date.toDateString() === yesterday.toDateString()
          ? "Yesterday"
          : date.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
    const last = groups.at(-1);
    if (last?.label === label) last.jobs.push(job);
    else groups.push({ label, jobs: [job] });
  }
  return groups;
}
