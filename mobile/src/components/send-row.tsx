import type { SendJob, SendStatus } from "@/api/schemas";
import { StatusBadge, Txt } from "@/components/ui";
import { formatMailTime } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import { MailX, MousePointerClick } from "lucide-react-native";
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

// Same rules as the web app's lib/send-summary.ts: bounced recipients stay in the
// total and count as accepted (accepted is not delivered).
function summary(job: SendJob) {
  const c = job.recipientCounts;
  const total = c.pending + c.sending + c.sent + c.failed + c.cancelled + c.bounced;
  if (job.isTest) return "test send";
  if (job.isReply) return "reply";
  if (total === 1) return "1 recipient";
  return `${c.sent + c.bounced} of ${total} accepted`;
}

/** Bounces from delivery reports and clicks by people (not link scanners); clicks only for tracked sends that went out. */
function Outcomes({ job }: { job: SendJob }) {
  const { theme: t } = useTheme();
  const c = job.recipientCounts;
  const bounced = c.bounced;
  const clicked = job.trackClicks && c.sent + c.bounced > 0 ? job.uniqueClickers : null;
  if (bounced === 0 && clicked === null) return null;

  const clickInk = clicked ? t.color.info : t.color.muted;
  return (
    <View style={{ flexDirection: "row", flexWrap: "wrap", columnGap: 12, marginTop: 4 }}>
      {bounced > 0 && (
        <View style={{ flexDirection: "row", alignItems: "center", gap: 4 }}>
          <MailX size={13} color={t.color.warning} />
          <Txt v="mono" style={{ color: t.color.warning }}>
            {bounced} bounced
          </Txt>
        </View>
      )}
      {clicked !== null && (
        <View style={{ flexDirection: "row", alignItems: "center", gap: 4 }}>
          <MousePointerClick size={13} color={clickInk} />
          <Txt v="mono" style={{ color: clickInk }}>
            {clicked > 0 ? `${clicked} clicked` : "no clicks yet"}
          </Txt>
        </View>
      )}
    </View>
  );
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
        <Outcomes job={job} />
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
