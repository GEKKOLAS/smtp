"use client";

import { listAccounts } from "@/lib/api/accounts";
import { listInbox } from "@/lib/api/inbox";
import { listSends } from "@/lib/api/sends";
import { listTemplates } from "@/lib/api/templates";
import { useSession } from "@/lib/hooks/use-session";
import { queryKeys } from "@/lib/query/query-keys";
import type { SendJob } from "@/lib/schemas/sends";
import { StampAvatar } from "@/components/app/stamp-avatar";
import { isActiveStatus } from "@/components/sends/send-status-badge";
import { SendsTable } from "@/components/sends/sends-table";
import { TemplatePreviewThumb } from "@/components/templates/template-preview-thumb";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { formatDate, formatMailTime } from "@/lib/format";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, ArrowRight, Check, Plus, Reply } from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";

const CHART_DAYS = 14;

function greeting(hour: number) {
  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";
  return "Good evening";
}

function localDayKey(date: Date) {
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
}

/** Recipients per local day for the last CHART_DAYS days, built from the loaded send jobs. */
function dailyVolume(jobs: SendJob[]) {
  const today = new Date();
  const days = Array.from({ length: CHART_DAYS }, (_, i) => {
    const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() - (CHART_DAYS - 1 - i));
    return {
      key: localDayKey(date),
      label: date.toLocaleDateString(undefined, { month: "short", day: "numeric" }),
      sent: 0,
      failed: 0,
    };
  });
  const byKey = new Map(days.map((day) => [day.key, day]));
  for (const job of jobs) {
    const day = byKey.get(localDayKey(new Date(job.createdAt)));
    if (!day) continue;
    day.sent += job.recipientCounts.sent;
    day.failed += job.recipientCounts.failed;
  }
  return days;
}

// Light theme gives each KPI its own flat color block; dark theme keeps them all on the card surface.
const TILE_TONES = {
  lime: "bg-primary text-primary-foreground",
  white: "bg-card text-card-foreground",
  orange: "bg-[#ff7a45] text-[#111111]",
  violet: "bg-[#7c5cff] text-white",
};

function KpiTile({
  tone,
  label,
  scope,
  value,
  children,
}: {
  tone: keyof typeof TILE_TONES;
  label: string;
  scope: string;
  value: string;
  children?: ReactNode;
}) {
  return (
    <div
      className={`flex min-h-36 flex-col justify-between gap-3 rounded-xl border-2 p-4 shadow-hard dark:border dark:bg-card dark:text-card-foreground dark:shadow-none ${TILE_TONES[tone]}`}
    >
      <div className="flex items-center justify-between gap-2">
        <p className="text-[13px] font-semibold dark:font-medium dark:text-muted-foreground">{label}</p>
        <span className="font-mono text-[11px] font-bold uppercase opacity-80 dark:font-medium dark:text-muted-foreground dark:opacity-100">
          {scope}
        </span>
      </div>
      <p className="font-heading text-[2.6rem] leading-none font-bold tracking-[-0.04em] tabular-nums dark:text-[1.75rem] dark:font-semibold dark:tracking-tight">
        {value}
      </p>
      {children}
    </div>
  );
}

function Meter({ percent, fill }: { percent: number; fill: string }) {
  return (
    <div
      aria-hidden
      className="h-2.5 overflow-hidden rounded-full border-2 border-current dark:h-1.5 dark:border-0 dark:bg-muted"
    >
      <div className={`h-full bg-current ${fill}`} style={{ width: `${Math.min(100, Math.max(percent, 2))}%` }} />
    </div>
  );
}

function SectionTitle({ children, href, linkLabel }: { children: ReactNode; href?: string; linkLabel?: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <h2 className="font-heading text-[1.4rem] font-bold tracking-[-0.02em] dark:text-[15px] dark:font-semibold dark:tracking-normal">
        {children}
      </h2>
      {href && (
        <Link href={href} className="text-[13px] font-semibold text-link hover:underline">
          {linkLabel}
        </Link>
      )}
    </div>
  );
}

export default function DashboardPage() {
  const { data: user } = useSession();
  const { data: accounts } = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const { data: sends } = useQuery({ queryKey: queryKeys.sends(), queryFn: () => listSends() });
  const { data: templates } = useQuery({
    queryKey: queryKeys.templates({ search: "", archived: false }),
    queryFn: () => listTemplates({ archived: false }),
  });

  const inboxAccount =
    accounts?.find((a) => a.isDefault && a.canReadInbox && a.state === "active") ??
    accounts?.find((a) => a.canReadInbox && a.state === "active");
  const { data: inbox, isLoading: inboxLoading } = useQuery({
    queryKey: queryKeys.inbox(inboxAccount?.id ?? "", ""),
    queryFn: () => listInbox({ accountId: inboxAccount!.id }),
    enabled: !!inboxAccount,
  });

  const jobs = sends?.items ?? [];
  const totals = jobs.reduce(
    (acc, job) => ({ sent: acc.sent + job.recipientCounts.sent, failed: acc.failed + job.recipientCounts.failed }),
    { sent: 0, failed: 0 },
  );
  const attempted = totals.sent + totals.failed;
  const deliveryRate = attempted > 0 ? (totals.sent / attempted) * 100 : null;
  const scope = sends && sends.totalCount > jobs.length ? `Last ${jobs.length}` : "All time";
  const inProgress = jobs.filter((job) => isActiveStatus(job.status)).length;
  const unread = inbox?.items.filter((m) => !m.isRead) ?? [];
  const days = dailyVolume(jobs);
  const maxDay = Math.max(1, ...days.map((d) => d.sent + d.failed));
  const chartTotal = days.reduce((sum, d) => sum + d.sent + d.failed, 0);
  const accountEmail = new Map(accounts?.map((a) => [a.id, a.emailAddress]));
  const needsReconnect = accounts?.filter((a) => a.state === "needs_reconnect") ?? [];

  const steps = [
    { label: "Connect an account", done: (accounts?.length ?? 0) > 0, href: "/accounts", cta: "Connect account" },
    { label: "Create a template", done: (templates?.totalCount ?? 0) > 0, href: "/templates", cta: "New template" },
    { label: "Send a test", done: (sends?.totalCount ?? 0) > 0, href: "/compose", cta: "Send a test" },
  ];
  const setupLoaded = !!accounts && !!templates && !!sends;
  const nextStep = steps.find((step) => !step.done);
  const doneCount = steps.filter((step) => step.done).length;

  const now = new Date();
  const firstName = user?.displayName.trim().split(/\s+/)[0];
  const eyebrow = [
    now.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" }),
    inbox && `${unread.length} unread ${unread.length === 1 ? "reply" : "replies"}`,
    inProgress > 0 && `${inProgress} ${inProgress === 1 ? "send" : "sends"} in progress`,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <div className="mx-auto max-w-6xl space-y-7">
      <header>
        <p className="mb-2 font-mono text-xs font-bold tracking-[0.04em] uppercase dark:font-medium dark:text-muted-foreground">
          {eyebrow}
        </p>
        <h1 className="font-heading text-5xl leading-[1.05] font-bold tracking-[-0.04em] dark:text-[1.9rem] dark:font-semibold dark:tracking-tight">
          {greeting(now.getHours())}
          {firstName ? `, ${firstName}` : ""}.
        </h1>
      </header>

      {needsReconnect.length > 0 && (
        <section
          aria-label="Needs attention"
          className="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-xl border-2 bg-[#ff7a45] px-4 py-3.5 text-[#111111] shadow-hard dark:border dark:border-warning/35 dark:bg-warning-soft dark:text-foreground dark:shadow-none"
        >
          <AlertTriangle className="size-5 shrink-0 dark:text-warning" />
          <p className="min-w-60 flex-1 text-sm">
            <span className="font-bold">
              {needsReconnect.length === 1
                ? `${needsReconnect[0].emailAddress} needs to be reconnected.`
                : `${needsReconnect.length} accounts need to be reconnected.`}
            </span>{" "}
            Sends from {needsReconnect.length === 1 ? "it" : "them"} are paused until you sign in again.
          </p>
          <Button asChild variant="outline" size="sm">
            <Link href="/accounts">Review accounts</Link>
          </Button>
        </section>
      )}

      {setupLoaded && nextStep && (
        <section
          aria-label="Workspace setup"
          className="flex flex-wrap items-center gap-x-6 gap-y-3 rounded-xl border-2 border-[#111111] bg-[#111111] py-3 pr-3 pl-4 text-white dark:border dark:border-border dark:bg-card dark:text-card-foreground"
        >
          <div className="w-48">
            <p className="text-[13px] font-bold">Workspace setup</p>
            <div className="mt-2 flex items-center gap-2.5">
              <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/20 dark:bg-muted">
                <div className="h-full rounded-full bg-primary" style={{ width: `${(doneCount / steps.length) * 100}%` }} />
              </div>
              <span className="font-mono text-xs font-bold">
                {doneCount}/{steps.length}
              </span>
            </div>
          </div>
          <ol className="flex flex-1 flex-wrap gap-2">
            {steps.map((step) =>
              step.done ? (
                <li
                  key={step.label}
                  className="flex h-7 items-center gap-1.5 rounded-full px-2.5 text-[13px] text-white/50 line-through dark:bg-success-soft dark:text-success dark:no-underline"
                >
                  <Check className="size-3.5" strokeWidth={2.5} />
                  {step.label}
                </li>
              ) : (
                <li
                  key={step.label}
                  className={`flex h-7 items-center gap-1.5 rounded-full px-2.5 text-[13px] ${step === nextStep ? "font-bold" : "text-white/70 dark:text-muted-foreground"}`}
                >
                  {step === nextStep && <ArrowRight className="size-3.5" />}
                  {step.label}
                </li>
              ),
            )}
          </ol>
          <Button asChild variant="gradient" size="sm">
            <Link href={nextStep.href}>{nextStep.cta}</Link>
          </Button>
        </section>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiTile tone="lime" label="Emails sent" scope={scope} value={totals.sent.toLocaleString()}>
          <div aria-hidden className="flex h-7 items-end gap-[3px]">
            {days.slice(-10).map((day, i, arr) => (
              <div
                key={day.key}
                className={`w-[5px] rounded-sm bg-current dark:bg-primary ${i < arr.length - 3 ? "opacity-40" : ""}`}
                style={{ height: `${Math.max(12, ((day.sent + day.failed) / maxDay) * 100)}%` }}
              />
            ))}
          </div>
        </KpiTile>
        <KpiTile
          tone="white"
          label="Delivery rate"
          scope={scope}
          value={deliveryRate === null ? "—" : `${deliveryRate.toFixed(1)}%`}
        >
          <div className="space-y-1.5">
            <Meter percent={deliveryRate ?? 0} fill="dark:bg-success" />
            <p className="text-xs opacity-80 dark:text-muted-foreground dark:opacity-100">
              {totals.sent.toLocaleString()} of {attempted.toLocaleString()} delivered
            </p>
          </div>
        </KpiTile>
        <KpiTile tone="orange" label="Failed" scope={scope} value={totals.failed.toLocaleString()}>
          <div className="space-y-1.5">
            <Meter percent={attempted > 0 ? (totals.failed / attempted) * 100 : 0} fill="dark:bg-destructive" />
            <p className="text-xs opacity-80 dark:text-muted-foreground dark:opacity-100">
              {attempted > 0 ? `${((totals.failed / attempted) * 100).toFixed(1)}% of recipients` : "Nothing sent yet"}
            </p>
          </div>
        </KpiTile>
        <KpiTile tone="violet" label="Unread replies" scope="Now" value={inbox ? String(unread.length) : "—"}>
          <p className="text-xs opacity-85 dark:text-muted-foreground dark:opacity-100">
            {inboxAccount ? `In ${inboxAccount.emailAddress}` : "No mailbox with inbox access"}
          </p>
        </KpiTile>
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.7fr)_minmax(0,1fr)]">
        <Card className="gap-4 px-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="font-heading text-[1.4rem] font-bold tracking-[-0.02em] dark:text-[15px] dark:font-semibold dark:tracking-normal">
                Send volume
              </h2>
              <p className="text-xs text-muted-foreground">
                Recipients per day · last {CHART_DAYS} days
                {sends && sends.totalCount > jobs.length ? ` · from your latest ${jobs.length} sends` : ""}
              </p>
            </div>
            <div className="flex gap-3.5 text-xs text-muted-foreground">
              <span className="flex items-center gap-1.5">
                <span className="size-2.5 rounded-sm border-2 bg-primary dark:border-0" />
                Delivered
              </span>
              <span className="flex items-center gap-1.5">
                <span className="size-2.5 rounded-sm border-2 bg-chart-2 dark:border-0 dark:bg-destructive" />
                Failed
              </span>
              <span className="flex items-center gap-1.5 dark:hidden">
                <span className="size-2.5 rounded-sm border-2 bg-chart-3" />
                Today
              </span>
            </div>
          </div>
          {chartTotal === 0 ? (
            <div className="flex h-44 items-center justify-center rounded-lg border-2 border-dashed text-sm text-muted-foreground dark:border">
              Nothing sent in the last {CHART_DAYS} days.
            </div>
          ) : (
            <div>
              <div
                role="img"
                aria-label={`${chartTotal} recipients over the last ${CHART_DAYS} days`}
                className="flex h-44 items-end gap-1.5 border-b-2 px-1 dark:border-b dark:border-border"
              >
                {days.map((day, i) => {
                  const total = day.sent + day.failed;
                  const isToday = i === days.length - 1;
                  return (
                    <div
                      key={day.key}
                      title={`${day.label}: ${day.sent} delivered, ${day.failed} failed`}
                      className="flex h-full flex-1 flex-col justify-end"
                    >
                      {total > 0 && (
                        <div
                          className="flex flex-col overflow-hidden rounded-t-md border-2 border-b-0 dark:border-0"
                          style={{ height: `${Math.max(4, (total / maxDay) * 100)}%` }}
                        >
                          {day.failed > 0 && (
                            <div
                              className="shrink-0 bg-chart-2 dark:bg-destructive"
                              style={{ height: `${(day.failed / total) * 100}%` }}
                            />
                          )}
                          <div className={`flex-1 ${isToday ? "bg-chart-3 dark:bg-primary" : "bg-primary dark:bg-primary/75"}`} />
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
              <div aria-hidden className="mt-2 flex justify-between font-mono text-[11px] text-muted-foreground">
                <span>{days[0].label}</span>
                <span>{days[Math.floor(CHART_DAYS / 2)].label}</span>
                <span className="font-bold text-foreground">Today</span>
              </div>
            </div>
          )}
        </Card>

        <Card className="gap-0 pb-0">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-[1.4rem] tracking-[-0.02em] dark:text-[15px] dark:tracking-normal">Inbox</CardTitle>
            {inbox && (
              <Link href="/inbox" className="text-[13px] font-semibold text-link hover:underline">
                {unread.length} unread →
              </Link>
            )}
          </CardHeader>
          <CardContent className="flex-1 px-2">
            {!inboxAccount ? (
              <p className="px-2 py-6 text-sm text-muted-foreground">
                Connect a mailbox with inbox access to see replies here.{" "}
                <Link href="/accounts" className="font-semibold text-link hover:underline">
                  Accounts
                </Link>
              </p>
            ) : inboxLoading ? (
              <p className="px-2 py-6 text-sm text-muted-foreground">Loading messages…</p>
            ) : !inbox || inbox.items.length === 0 ? (
              <p className="px-2 py-6 text-sm text-muted-foreground">No messages yet.</p>
            ) : (
              <ul className="flex flex-col gap-0.5">
                {inbox.items.slice(0, 3).map((message) => {
                  const sender = message.fromName || message.fromEmail;
                  return (
                    <li key={message.id}>
                      <Link
                        href="/inbox"
                        className="flex gap-2.5 rounded-lg px-2 py-2.5 transition-colors hover:bg-accent"
                      >
                        <StampAvatar name={sender} />
                        <span className="min-w-0 flex-1">
                          <span className="flex items-center justify-between gap-2">
                            <span className={`truncate text-[13px] ${message.isRead ? "text-muted-foreground" : "font-bold"}`}>
                              {sender}
                            </span>
                            <span className="flex shrink-0 items-center gap-1.5 font-mono text-[11px] text-muted-foreground">
                              {!message.isRead && (
                                <span className="size-1.5 rounded-full bg-[#ff7a45] dark:bg-primary" aria-label="Unread" />
                              )}
                              {formatMailTime(message.receivedAt)}
                            </span>
                          </span>
                          <span className={`block truncate text-[13px] ${message.isRead ? "text-muted-foreground" : ""}`}>
                            {message.subject || "(no subject)"}
                          </span>
                          <span className="block truncate text-xs text-muted-foreground">{message.snippet}</span>
                        </span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            )}
          </CardContent>
          {inbox && inbox.items.length > 0 && (
            <div className="p-4 pt-3">
              <Button asChild variant="secondary" className="w-full">
                <Link href="/inbox">
                  <Reply />
                  Reply with a template
                </Link>
              </Button>
            </div>
          )}
        </Card>
      </div>

      <section className="space-y-3.5">
        <SectionTitle href="/templates" linkLabel="View all →">
          Templates
        </SectionTitle>
        <ul className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {templates?.items.slice(0, 3).map((template) => (
            <li key={template.id} className="odd:-rotate-[0.6deg] even:rotate-[0.5deg] dark:odd:rotate-0 dark:even:rotate-0">
              <Link
                href={`/templates/${template.id}/edit`}
                className="block overflow-hidden rounded-xl border-2 bg-card shadow-hard press dark:border dark:shadow-none dark:hover:border-input"
              >
                <div className="border-b-2 dark:border-b">
                  <TemplatePreviewThumb versionId={template.currentVersionId} />
                </div>
                <div className="px-3.5 py-3">
                  <p className="truncate text-sm font-bold dark:font-semibold">{template.name}</p>
                  <p className="font-mono text-[11px] text-muted-foreground">
                    {template.currentVersionNumber ? `v${template.currentVersionNumber} · ` : ""}updated{" "}
                    {formatDate(template.updatedAt)}
                  </p>
                </div>
              </Link>
            </li>
          ))}
          <li>
            <Link
              href="/templates"
              className="flex h-full min-h-56 flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed p-5 text-center transition-colors hover:bg-card/50 dark:border dark:border-input"
            >
              <span className="flex size-11 items-center justify-center rounded-full border-2 bg-primary text-primary-foreground shadow-hard-sm dark:border-0 dark:bg-primary/15 dark:text-primary dark:shadow-none">
                <Plus className="size-5" strokeWidth={2.5} />
              </span>
              <span>
                <span className="block text-sm font-bold">New template</span>
                <span className="block text-xs text-muted-foreground">Start visually, in MJML, or with AI</span>
              </span>
            </Link>
          </li>
        </ul>
      </section>

      <Card className="gap-0 py-0">
        <div className="flex items-center justify-between px-4 py-3.5">
          <SectionTitle>Recent sends</SectionTitle>
          {jobs.length > 0 && (
            <Link href="/sends" className="text-[13px] font-semibold text-link hover:underline">
              View history →
            </Link>
          )}
        </div>
        {jobs.length === 0 ? (
          <p className="border-t-2 py-8 text-center text-sm text-muted-foreground dark:border-t">
            Nothing sent yet.{" "}
            <Link href="/compose" className="font-semibold text-foreground hover:underline">
              Compose an email
            </Link>
            .
          </p>
        ) : (
          <SendsTable jobs={jobs.slice(0, 5)} accountEmail={accountEmail} />
        )}
      </Card>
    </div>
  );
}
