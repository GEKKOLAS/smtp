import type { SendJob } from "@/lib/schemas/sends";
import { SendStatusBadge } from "@/components/sends/send-status-badge";
import { formatDate } from "@/lib/format";
import Link from "next/link";

const TH =
  "border-y-2 px-4 py-2 font-mono text-[11px] font-bold tracking-wide uppercase dark:border-y dark:font-sans dark:text-xs dark:font-medium dark:tracking-normal dark:normal-case";
const TD = "border-b-2 px-4 py-3 dark:border-b dark:border-border/60";

function totalRecipients(job: SendJob) {
  const c = job.recipientCounts;
  return c.pending + c.sending + c.sent + c.failed + c.cancelled;
}

/** Send jobs as table rows. Pass `accountEmail` to show which mailbox each one went out from. */
export function SendsTable({ jobs, accountEmail }: { jobs: SendJob[]; accountEmail?: Map<string, string> }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[40rem] border-collapse text-[13px]">
        <thead>
          <tr className="text-left text-muted-foreground">
            <th className={TH}>Subject</th>
            {accountEmail && <th className={TH}>From</th>}
            <th className={`${TH} text-right`}>Delivered</th>
            <th className={TH}>Status</th>
            <th className={`${TH} text-right`}>Date</th>
          </tr>
        </thead>
        <tbody className="[&_tr:last-child_td]:border-b-0">
          {jobs.map((job) => (
            <tr key={job.id} className="transition-colors hover:bg-accent">
              <td className={`${TD} max-w-80`}>
                <Link href={`/sends/${job.id}`} className="block truncate font-semibold hover:underline">
                  {job.subjectSnapshot}
                  {job.isTest && <span className="ml-1.5 font-normal text-muted-foreground">(test)</span>}
                  {job.isReply && <span className="ml-1.5 font-normal text-muted-foreground">(reply)</span>}
                </Link>
              </td>
              {accountEmail && <td className={`${TD} text-muted-foreground`}>{accountEmail.get(job.accountId) ?? "—"}</td>}
              <td className={`${TD} text-right font-mono`}>
                {job.recipientCounts.sent}
                <span className="text-muted-foreground">/{totalRecipients(job)}</span>
                {job.recipientCounts.failed > 0 && (
                  <span className="ml-1.5 text-destructive">{job.recipientCounts.failed} failed</span>
                )}
              </td>
              <td className={TD}>
                <SendStatusBadge status={job.status} />
              </td>
              <td className={`${TD} text-right whitespace-nowrap text-muted-foreground`}>
                {job.scheduledAt && job.status === "scheduled"
                  ? `Scheduled ${formatDate(job.scheduledAt)}`
                  : formatDate(job.createdAt)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
