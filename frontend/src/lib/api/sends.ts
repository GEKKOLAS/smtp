import { api } from "@/lib/api/client";
import {
  pagedSendsSchema,
  type SendJob,
  sendJobSchema,
  type SendJobDetail,
  sendJobDetailSchema,
} from "@/lib/schemas/sends";

export interface RecipientInput {
  email: string;
  name?: string | null;
  contactId?: string | null;
  variableOverrides?: Record<string, string | null> | null;
}

export interface CreateSendInput {
  connectedEmailAccountId: string;
  templateVersionId: string;
  recipients: RecipientInput[];
  variables?: Record<string, string | null>;
  attachments?: { assetId: string; disposition: string; filenameOverride?: string | null }[];
  scheduledAt?: string | null;
}

export function createSend(input: CreateSendInput): Promise<SendJob> {
  return api("/sends", { body: input, schema: sendJobSchema });
}

export function testSend(input: {
  connectedEmailAccountId: string;
  templateVersionId: string;
  variables?: Record<string, string | null>;
  toSelf?: "login" | "account";
}): Promise<SendJob> {
  return api("/sends/test", { body: input, schema: sendJobSchema });
}

export function listSends(params: { status?: string; page?: number } = {}) {
  const query = new URLSearchParams();
  if (params.status) query.set("status", params.status);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", "20");
  return api(`/sends?${query.toString()}`, { schema: pagedSendsSchema });
}

export function getSend(id: string): Promise<SendJobDetail> {
  return api(`/sends/${id}`, { schema: sendJobDetailSchema });
}

export function cancelSend(id: string): Promise<SendJob> {
  return api(`/sends/${id}/cancel`, { method: "POST", schema: sendJobSchema });
}

export function retrySend(id: string): Promise<SendJob> {
  return api(`/sends/${id}/retry`, { method: "POST", schema: sendJobSchema });
}
