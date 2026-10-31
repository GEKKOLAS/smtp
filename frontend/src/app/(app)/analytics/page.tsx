"use client";

import {
  ANALYTICS_RANGES,
  type AnalyticsRange,
  formatRate,
  getAnalyticsSummary,
} from "@/lib/api/analytics";
import { queryKeys } from "@/lib/query/query-keys";
import { DailyChart, DailyLegend } from "@/components/analytics/daily-chart";
import { PageHeader } from "@/components/app/page-header";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useQuery } from "@tanstack/react-query";
import { BarChart3 } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

function Tile({ label, value, detail }: { label: string; value: string; detail: string }) {
  return (
    <div className="min-w-0 rounded-xl border-2 bg-card p-3.5 shadow-hard-sm sm:p-4 dark:border dark:shadow-none">
      <p className="font-mono text-[11px] font-bold tracking-[0.06em] text-muted-foreground uppercase dark:font-sans dark:font-medium dark:normal-case dark:tracking-normal">
        {label}
      </p>
      <p className="mt-1 font-heading text-2xl font-bold tracking-tight tabular-nums sm:text-3xl dark:font-semibold">{value}</p>
      <p className="mt-0.5 text-xs text-muted-foreground">{detail}</p>
    </div>
  );
}

export default function AnalyticsPage() {
  const [days, setDays] = useState<AnalyticsRange>(30);
  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.analytics(days),
    queryFn: () => getAnalyticsSummary(days),
  });
  const totals = data?.totals;

  return (
    <div className="mx-auto max-w-6xl space-y-7">
      <PageHeader
        icon={BarChart3}
        eyebrow={`Last ${days} days`}
        title="Analytics"
        description="What your sends did, counted honestly: no open tracking, and link scanners don't count as clicks."
        action={
          <div className="flex gap-1.5" role="radiogroup" aria-label="Date range">
            {ANALYTICS_RANGES.map((range) => (
              <Button
                key={range}
                size="sm"
                role="radio"
                aria-checked={days === range}
                variant={days === range ? "default" : "outline"}
                onClick={() => setDays(range)}
              >
                {range} days
              </Button>
            ))}
          </div>
        }
      />

      {isLoading && <p className="text-sm text-muted-foreground">Loading analytics…</p>}
      {isError && <p className="text-sm text-destructive">Could not load analytics.</p>}

      {data && totals && (
        <>
          <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
            <Tile label="Sends" value={String(totals.sends)} detail={`${totals.recipients} recipients`} />
            <Tile
              label="Accepted"
              value={String(totals.accepted)}
              detail={totals.failed > 0 ? `${totals.failed} rejected at send time` : "Taken by Gmail or Outlook"}
            />
            <Tile
              label="Bounce rate"
              value={formatRate(totals.bounceRate)}
              detail={`${totals.bounced} bounced after being accepted`}
            />
            <Tile
              label="Click rate"
              value={formatRate(totals.clickRate)}
              detail={
                totals.trackedRecipients > 0
                  ? `${totals.uniqueClickers} of ${totals.trackedRecipients} tracked recipients clicked`
                  : "No tracked sends in this range"
              }
            />
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Recipients per day</CardTitle>
              <CardDescription>By outcome, on the day each message went out.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <DailyLegend />
              {totals.recipients === 0 ? (
                <p className="py-10 text-center text-sm text-muted-foreground">
                  Nothing sent in this range yet.{" "}
                  <Link href="/compose" className="font-semibold text-link hover:underline">
                    Compose a send
                  </Link>
                </p>
              ) : (
                <DailyChart points={data.daily} />
              )}
              <details className="text-sm">
                <summary className="cursor-pointer text-xs font-semibold text-muted-foreground">View as table</summary>
                <table className="mt-2 w-full text-left text-xs">
                  <thead className="text-muted-foreground">
                    <tr>
                      <th className="py-1 font-medium">Day</th>
                      <th className="py-1 text-right font-medium">Accepted</th>
                      <th className="py-1 text-right font-medium">Bounced</th>
                      <th className="py-1 text-right font-medium">Failed</th>
                    </tr>
                  </thead>
                  <tbody className="font-mono tabular-nums">
                    {data.daily
                      .filter((d) => d.accepted + d.bounced + d.failed > 0)
                      .map((d) => (
                        <tr key={d.date} className="border-t border-border/60">
                          <td className="py-1">{d.date}</td>
                          <td className="py-1 text-right">{d.accepted}</td>
                          <td className="py-1 text-right">{d.bounced}</td>
                          <td className="py-1 text-right">{d.failed}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </details>
            </CardContent>
          </Card>

          <div className="grid gap-6 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
            <Card className="gap-0 pb-0">
              <CardHeader className="pb-3">
                <CardTitle>By template</CardTitle>
              </CardHeader>
              <CardContent className="overflow-x-auto p-0">
                {data.templates.length === 0 ? (
                  <p className="px-4 pb-4 text-sm text-muted-foreground">No sends yet.</p>
                ) : (
                  <table className="w-full min-w-[30rem] text-left text-sm">
                    <thead className="border-t-2 font-mono text-[11px] text-muted-foreground uppercase dark:border-t dark:font-sans dark:normal-case">
                      <tr>
                        <th className="px-4 py-2 font-medium">Template</th>
                        <th className="px-2 py-2 text-right font-medium">Recipients</th>
                        <th className="px-2 py-2 text-right font-medium">Bounce rate</th>
                        <th className="px-4 py-2 text-right font-medium">Click rate</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y-2 border-t-2 dark:divide-y dark:divide-border/60 dark:border-t">
                      {data.templates.map((t) => (
                        <tr key={t.templateId}>
                          <td className="max-w-56 truncate px-4 py-2.5 font-medium">{t.name}</td>
                          <td className="px-2 py-2.5 text-right font-mono tabular-nums">{t.recipients}</td>
                          <td className="px-2 py-2.5 text-right font-mono tabular-nums">{formatRate(t.bounceRate)}</td>
                          <td className="px-4 py-2.5 text-right font-mono tabular-nums">{formatRate(t.clickRate)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Top links</CardTitle>
                <CardDescription>Clicks by people, from tracked sends.</CardDescription>
              </CardHeader>
              <CardContent>
                {data.topLinks.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No clicks yet. Turn on &ldquo;Track clicks&rdquo; when you compose a send.
                  </p>
                ) : (
                  <ol className="space-y-2.5">
                    {data.topLinks.map((link) => (
                      <li key={link.url} className="flex items-baseline gap-3 text-sm">
                        <span className="min-w-0 flex-1 truncate" title={link.url}>
                          {link.url.replace(/^https?:\/\//, "")}
                        </span>
                        <span className="shrink-0 font-mono text-xs text-muted-foreground tabular-nums">
                          {link.clicks} clicks · {link.uniqueClickers} people
                        </span>
                      </li>
                    ))}
                  </ol>
                )}
              </CardContent>
            </Card>
          </div>

          <p className="text-xs text-muted-foreground">
            Accepted means the provider took the message, not that it reached the inbox. Bounces are read from delivery
            reports in your mailbox, so they need inbox access. Conversions belong in your site&apos;s analytics: add a UTM
            campaign when you compose.
          </p>
        </>
      )}
    </div>
  );
}
