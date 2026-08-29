import { api } from "@/lib/api/client";
import {
  type InboxMessageDetail,
  inboxMessageDetailSchema,
  type InboxPage,
  inboxPageSchema,
} from "@/lib/schemas/inbox";

export function listInbox(params: {
  accountId: string;
  pageToken?: string | null;
  search?: string;
}): Promise<InboxPage> {
  const query = new URLSearchParams({ accountId: params.accountId, pageSize: "25" });
  if (params.pageToken) query.set("pageToken", params.pageToken);
  if (params.search) query.set("search", params.search);
  return api(`/inbox?${query.toString()}`, { schema: inboxPageSchema });
}

export function getInboxMessage(accountId: string, messageId: string): Promise<InboxMessageDetail> {
  const query = new URLSearchParams({ accountId }).toString();
  return api(`/inbox/${encodeURIComponent(messageId)}?${query}`, {
    schema: inboxMessageDetailSchema,
  });
}
