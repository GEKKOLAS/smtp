import { z } from "zod";

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
export type InboxPage = z.infer<typeof inboxPageSchema>;

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
