import { z } from "zod";

// The subset of the web client's schemas (frontend/src/lib/schemas) the phone uses.

export const userSchema = z.object({
  id: z.string(),
  email: z.string(),
  displayName: z.string(),
  defaultAccountId: z.string().nullable(),
  createdAt: z.string(),
});
export type User = z.infer<typeof userSchema>;

export const loginResponseSchema = z.object({ user: userSchema, csrfToken: z.string() });

export const accountSchema = z.object({
  id: z.string(),
  provider: z.enum(["gmail", "outlook"]),
  emailAddress: z.string(),
  state: z.enum(["active", "needs_reconnect", "revoked"]),
  isDefault: z.boolean(),
  canReadInbox: z.boolean(),
});
export type Account = z.infer<typeof accountSchema>;

export const inboxMessageSchema = z.object({
  id: z.string(),
  threadId: z.string().nullable(),
  fromEmail: z.string(),
  fromName: z.string().nullable(),
  subject: z.string(),
  receivedAt: z.string().nullable(),
  snippet: z.string(),
  isRead: z.boolean(),
});
export type InboxMessage = z.infer<typeof inboxMessageSchema>;

export const inboxPageSchema = z.object({
  items: z.array(inboxMessageSchema),
  nextPageToken: z.string().nullable(),
});

export const inboxMessageDetailSchema = z.object({
  id: z.string(),
  threadId: z.string().nullable(),
  fromEmail: z.string(),
  fromName: z.string().nullable(),
  to: z.array(z.string()),
  subject: z.string(),
  receivedAt: z.string().nullable(),
  html: z.string().nullable(),
  text: z.string().nullable(),
  replySubject: z.string(),
});
export type InboxMessageDetail = z.infer<typeof inboxMessageDetailSchema>;

export const templateVariableSchema = z.object({
  name: z.string(),
  type: z.enum(["text", "url", "html"]),
  required: z.boolean(),
  default: z.string().nullable(),
  sample: z.string().nullable(),
});
export type TemplateVariable = z.infer<typeof templateVariableSchema>;

export const templateSummarySchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().nullable(),
  currentVersionNumber: z.number().nullable(),
  currentVersionId: z.string().nullable(),
  updatedAt: z.string(),
});
export type TemplateSummary = z.infer<typeof templateSummarySchema>;

export const pagedTemplatesSchema = z.object({
  items: z.array(templateSummarySchema),
  totalCount: z.number(),
});

export const templateSchema = z.object({
  id: z.string(),
  name: z.string(),
  currentVersion: z
    .object({
      id: z.string(),
      versionNumber: z.number(),
      subject: z.string(),
      variablesSchema: z.array(templateVariableSchema),
    })
    .nullable(),
});

export const previewResultSchema = z.object({
  subject: z.string(),
  html: z.string(),
});

export const sendStatusSchema = z.enum([
  "scheduled", "queued", "sending", "sent", "partiallyfailed", "failed", "retrying", "cancelled",
]);
export type SendStatus = z.infer<typeof sendStatusSchema>;

export const sendJobSchema = z.object({
  id: z.string(),
  status: sendStatusSchema,
  isTest: z.boolean(),
  accountId: z.string(),
  subjectSnapshot: z.string(),
  recipientCounts: z.object({
    pending: z.number(),
    sending: z.number(),
    sent: z.number(),
    failed: z.number(),
    cancelled: z.number(),
  }),
  scheduledAt: z.string().nullable(),
  createdAt: z.string(),
  failureCode: z.string().nullable(),
  isReply: z.boolean(),
});
export type SendJob = z.infer<typeof sendJobSchema>;

export const pagedSendsSchema = z.object({
  items: z.array(sendJobSchema),
  totalCount: z.number(),
});

export const sendJobDetailSchema = z.object({ job: sendJobSchema });
