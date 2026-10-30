import type { SendJob, SendStatus } from "@/api/schemas";
import { StatusBadge, Txt } from "@/components/ui";
import { formatMailTime } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import { View } from "react-native";

const DOT: Record<SendStatus, string | null> = {
  sent: null, // the theme's primary
  queued: "#5D9CF2",
  sending: "#5D9CF2",
  retrying: "#FBBF24",
  partiallyfailed: "#FBBF24",
  failed: "#FF7A45",
  scheduled: "#8F8FA3",
  cancelled: "#8F8FA3",
};

function summary(job: SendJob) {
  const c = job.recipientCounts;
  const total = c.pending + c.sending + c.sent + c.failed + c.cancelled;
  if (job.isTest) return "test send";
  if (job.isReply) return "reply";
  if (total === 1) return "1 recipient";
  return `${c.sent} of ${total} delivered`;
}

/** One send in an activity list: status dot, subject, when/how many, and the status stamp. */
export function SendRow({ job, first }: { job: SendJob; first?: boolean }) {
  const { theme: t } = useTheme();
  return (
    <View
      style={{
        flexDirection: "row",
        alignItems: "flex-start",
        gap: 12,
        paddingHorizontal: 14,
        paddingVertical: 12,
        borderTopWidth: first ? 0 : t.borderWidth,
        borderTopColor: t.color.hairline,
      }}
    >
      <View
        style={{
          width: 10,
          height: 10,
          marginTop: 5,
          borderRadius: 5,
          backgroundColor: DOT[job.status] ?? t.color.primary,
          borderWidth: t.name === "light" ? 2 : 0,
          borderColor: "#111111",
        }}
      />
      <View style={{ flex: 1 }}>
        <Txt v="label" numberOfLines={1}>
          {job.subjectSnapshot}
        </Txt>
        <Txt v="mono" numberOfLines={1} style={{ marginTop: 2 }}>
          {job.status === "scheduled" && job.scheduledAt ? `for ${formatMailTime(job.scheduledAt)}` : formatMailTime(job.createdAt)} ·{" "}
          {summary(job)}
        </Txt>
      </View>
      <StatusBadge status={job.status} />
    </View>
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
