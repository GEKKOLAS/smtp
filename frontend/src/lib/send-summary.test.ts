import { describe, expect, it } from "vitest";
import type { SendJob } from "@/lib/schemas/sends";
import { recipientTotal, sendOutcomes, sendSummary } from "@/lib/send-summary";

const counts = { pending: 0, sending: 0, sent: 0, failed: 0, cancelled: 0, bounced: 0 };

function job(overrides: Partial<SendJob> = {}, recipientCounts: Partial<typeof counts> = {}): SendJob {
  return {
    id: "j1",
    status: "sent",
    isTest: false,
    accountId: "a1",
    templateVersionId: "v1",
    subjectSnapshot: "Spring menu",
    recipientCounts: { ...counts, ...recipientCounts },
    scheduledAt: null,
    createdAt: "2026-10-31T12:00:00Z",
    completedAt: null,
    failureCode: null,
    isReply: false,
    replyToMessageId: null,
    trackClicks: false,
    utmCampaign: null,
    uniqueClickers: 0,
    ...overrides,
  };
}

describe("sendSummary", () => {
  it("keeps bounced recipients in the total and counts them as accepted", () => {
    const j = job({}, { sent: 3, bounced: 1, failed: 1 });
    expect(recipientTotal(j.recipientCounts)).toBe(5);
    expect(sendSummary(j)).toBe("4 of 5 accepted");
  });

  it("names tests, replies and single recipients", () => {
    expect(sendSummary(job({ isTest: true }, { sent: 1 }))).toBe("test send");
    expect(sendSummary(job({ isReply: true }, { sent: 1 }))).toBe("reply");
    expect(sendSummary(job({}, { bounced: 1 }))).toBe("1 recipient");
  });
});

describe("sendOutcomes", () => {
  it("reports clicks only for tracked sends that went out", () => {
    expect(sendOutcomes(job({ trackClicks: true, uniqueClickers: 2 }, { sent: 3 }))).toEqual({ bounced: 0, clicked: 2 });
    expect(sendOutcomes(job({ trackClicks: true }, { sent: 3 })).clicked).toBe(0);
    expect(sendOutcomes(job({ trackClicks: false }, { sent: 3 })).clicked).toBeNull();
    expect(sendOutcomes(job({ trackClicks: true, status: "scheduled" }, { pending: 3 })).clicked).toBeNull();
  });

  it("reports bounces whether or not the send was tracked", () => {
    expect(sendOutcomes(job({}, { sent: 2, bounced: 1 }))).toEqual({ bounced: 1, clicked: null });
  });
});
