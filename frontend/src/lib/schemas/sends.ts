import { z } from "zod";

export const sendStatusSchema = z.enum([
  "scheduled", "queued", "sending", "sent", "partiallyfailed", "failed", "retrying", "cancelled",
]);
export type SendStatus = z.infer<typeof sendStatusSchema>;

export const recipientCountsSchema = z.object({
  pending: z.number(),
  sending: z.number(),
  sent: z.number(),
  failed: z.number(),
  cancelled: z.number(),
});

export const sendJobSchema = z.object({
  id: z.string(),
  status: sendStatusSchema,
  isTest: z.boolean(),
  accountId: z.string(),
  templateVersionId: z.string(),
  subjectSnapshot: z.string(),
  recipientCounts: recipientCountsSchema,
  scheduledAt: z.string().nullable(),
  createdAt: z.string(),
  completedAt: z.string().nullable(),
  failureCode: z.string().nullable(),
});
export type SendJob = z.infer<typeof sendJobSchema>;

export const recipientSchema = z.object({
  id: z.string(),
  email: z.string(),
  displayName: z.string().nullable(),
  status: z.enum(["pending", "sending", "sent", "failed", "cancelled"]),
  attemptCount: z.number(),
  providerMessageId: z.string().nullable(),
  failureCode: z.string().nullable(),
  failureMessage: z.string().nullable(),
  nextAttemptAt: z.string().nullable(),
});

export const providerEventSchema = z.object({
  eventType: z.string(),
  httpStatus: z.number().nullable(),
  providerErrorCode: z.string().nullable(),
  createdAt: z.string(),
});

export const sendJobDetailSchema = z.object({
  job: sendJobSchema,
  recipients: z.array(recipientSchema),
  events: z.array(providerEventSchema),
});
export type SendJobDetail = z.infer<typeof sendJobDetailSchema>;

export const pagedSendsSchema = z.object({
  items: z.array(sendJobSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});
