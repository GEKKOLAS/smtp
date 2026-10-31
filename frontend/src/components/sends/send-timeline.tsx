import type { SendJob, SendStatus } from "@/lib/schemas/sends";
import { SendStatusBadge } from "@/components/sends/send-status-badge";
import { formatMailTime } from "@/lib/format";
import { sendOutcomes, sendSummary } from "@/lib/send-summary";
import { MailX, MousePointerClick } from "lucide-react";
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

/** Bounces from delivery reports and clicks by people (not link scanners), when there are any to show. */
function Outcomes({ job }: { job: SendJob }) {
  const { bounced, clicked } = sendOutcomes(job);
  if (bounced === 0 && clicked === null) return null;
  return (
    <span className="mt-1 flex flex-wrap gap-x-3 font-mono text-xs">
      {bounced > 0 && (
        <span className="inline-flex items-center gap-1 text-warning">
          <MailX aria-hidden className="size-3.5" />
          {bounced} bounced
        </span>
      )}
      {clicked !== null && (
        <span className={`inline-flex items-center gap-1 ${clicked > 0 ? "text-info" : "text-muted-foreground"}`}>
          <MousePointerClick aria-hidden className="size-3.5" />
          {clicked > 0 ? `${clicked} clicked` : "no clicks yet"}
        </span>
      )}
    </span>
  );
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
                · {sendSummary(job)}
              </span>
              <Outcomes job={job} />
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
