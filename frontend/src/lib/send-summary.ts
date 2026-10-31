import type { SendJob } from "@/lib/schemas/sends";

type Counts = SendJob["recipientCounts"];

/** Every recipient of a send, whatever happened to them. */
export function recipientTotal(c: Counts) {
  return c.pending + c.sending + c.sent + c.failed + c.cancelled + c.bounced;
}

/** What the provider took, including the ones that bounced later: accepted is not delivered. */
export function acceptedCount(c: Counts) {
  return c.sent + c.bounced;
}

/** The short "what happened" line under a send's subject. */
export function sendSummary(job: SendJob) {
  if (job.isTest) return "test send";
  if (job.isReply) return "reply";
  const total = recipientTotal(job.recipientCounts);
  if (total === 1) return "1 recipient";
  return `${acceptedCount(job.recipientCounts)} of ${total} accepted`;
}

/**
 * Bounces and clicks for a send's timeline row. `clicked` counts people (link
 * scanners excluded) and is null when the send wasn't tracked or nothing went out yet.
 */
export function sendOutcomes(job: SendJob) {
  return {
    bounced: job.recipientCounts.bounced,
    clicked: job.trackClicks && acceptedCount(job.recipientCounts) > 0 ? job.uniqueClickers : null,
  };
}
