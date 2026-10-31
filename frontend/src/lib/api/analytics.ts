import { api } from "@/lib/api/client";
import { z } from "zod";

const totalsSchema = z.object({
  sends: z.number(),
  recipients: z.number(),
  accepted: z.number(),
  failed: z.number(),
  bounced: z.number(),
  trackedRecipients: z.number(),
  uniqueClickers: z.number(),
  bounceRate: z.number().nullable(),
  clickRate: z.number().nullable(),
});
export type AnalyticsTotals = z.infer<typeof totalsSchema>;

const dailySchema = z.object({ date: z.string(), accepted: z.number(), failed: z.number(), bounced: z.number() });
export type DailyPoint = z.infer<typeof dailySchema>;

const summarySchema = z.object({
  from: z.string(),
  to: z.string(),
  totals: totalsSchema,
  daily: z.array(dailySchema),
  templates: z.array(
    z.object({
      templateId: z.string(),
      name: z.string(),
      sends: z.number(),
      recipients: z.number(),
      accepted: z.number(),
      bounced: z.number(),
      bounceRate: z.number().nullable(),
      trackedRecipients: z.number(),
      uniqueClickers: z.number(),
      clickRate: z.number().nullable(),
    }),
  ),
  topLinks: z.array(z.object({ url: z.string(), clicks: z.number(), uniqueClickers: z.number() })),
});
export type AnalyticsSummary = z.infer<typeof summarySchema>;

const sendAnalyticsSchema = z.object({
  trackClicks: z.boolean(),
  utmCampaign: z.string().nullable(),
  totals: totalsSchema,
  links: z.array(
    z.object({
      position: z.number(),
      url: z.string(),
      clicks: z.number(),
      uniqueClickers: z.number(),
      scannerClicks: z.number(),
    }),
  ),
  recipients: z.array(
    z.object({
      recipientId: z.string(),
      email: z.string(),
      status: z.string(),
      clicked: z.boolean(),
      firstClickAt: z.string().nullable(),
    }),
  ),
});
export type SendAnalytics = z.infer<typeof sendAnalyticsSchema>;

export const ANALYTICS_RANGES = [7, 30, 90] as const;
export type AnalyticsRange = (typeof ANALYTICS_RANGES)[number];

export function getAnalyticsSummary(days: AnalyticsRange): Promise<AnalyticsSummary> {
  return api(`/analytics/summary?days=${days}`, { schema: summarySchema });
}

export function getSendAnalytics(sendId: string): Promise<SendAnalytics> {
  return api(`/sends/${sendId}/analytics`, { schema: sendAnalyticsSchema });
}

/** "12%" / "12.5%", or an en dash when there is no base to divide by. */
export function formatRate(rate: number | null): string {
  if (rate === null) return "–";
  const percent = rate * 100;
  return `${percent >= 10 || Number.isInteger(percent) ? Math.round(percent) : percent.toFixed(1)}%`;
}
