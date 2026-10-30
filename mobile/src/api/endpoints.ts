import { api, setCsrfToken } from "@/api/client";
import {
  accountSchema,
  inboxMessageDetailSchema,
  inboxPageSchema,
  loginResponseSchema,
  pagedSendsSchema,
  pagedTemplatesSchema,
  previewResultSchema,
  sendJobDetailSchema,
  sendJobSchema,
  templateSchema,
  userSchema,
  type SendStatus,
} from "@/api/schemas";
import { z } from "zod";

export const queryKeys = {
  me: ["me"] as const,
  accounts: ["accounts"] as const,
  inbox: (accountId: string, search: string) => ["inbox", accountId, search] as const,
  inboxMessage: (accountId: string, id: string) => ["inbox-message", accountId, id] as const,
  templates: ["templates"] as const,
  template: (id: string) => ["template", id] as const,
  preview: (versionId: string) => ["preview", versionId] as const,
  sends: ["sends"] as const,
  send: (id: string) => ["send", id] as const,
};

export async function login(email: string, password: string) {
  const { user, csrfToken } = await api("/auth/login", { body: { email, password }, schema: loginResponseSchema });
  await setCsrfToken(csrfToken);
  return user;
}

export async function logout() {
  try {
    await api("/auth/logout", { method: "POST" });
  } finally {
    await setCsrfToken(null);
  }
}

export const getMe = () => api("/me", { schema: userSchema });

export const listAccounts = () =>
  api("/email-accounts", { schema: z.object({ items: z.array(accountSchema) }) }).then((r) => r.items);

export function listInbox(accountId: string, search: string) {
  const query = new URLSearchParams({ accountId, pageSize: "25" });
  if (search) query.set("search", search);
  return api(`/inbox?${query.toString()}`, { schema: inboxPageSchema });
}

export const getInboxMessage = (accountId: string, id: string) =>
  api(`/inbox/${encodeURIComponent(id)}?${new URLSearchParams({ accountId }).toString()}`, {
    schema: inboxMessageDetailSchema,
  });

export const listTemplates = () =>
  api("/templates?archived=false&page=1&pageSize=100", { schema: pagedTemplatesSchema });

export const getTemplate = (id: string) => api(`/templates/${id}`, { schema: templateSchema });

export const previewTemplate = (templateVersionId: string) =>
  api("/render/preview", { body: { templateVersionId, variables: {}, mode: "sample" }, schema: previewResultSchema });

export const replyWithTemplate = (input: {
  accountId: string;
  templateVersionId: string;
  messageId: string;
  variables: Record<string, string>;
  subjectOverride: string | null;
}) =>
  api("/sends", {
    body: {
      connectedEmailAccountId: input.accountId,
      templateVersionId: input.templateVersionId,
      variables: input.variables,
      replyToMessageId: input.messageId,
      subjectOverride: input.subjectOverride,
    },
    schema: sendJobSchema,
  });

export const getSend = (id: string) => api(`/sends/${id}`, { schema: sendJobDetailSchema }).then((r) => r.job);

export const listSends = () => api("/sends?page=1&pageSize=20", { schema: pagedSendsSchema });

const ACTIVE: SendStatus[] = ["queued", "sending", "retrying", "scheduled"];
export const isActiveStatus = (status: SendStatus) => ACTIVE.includes(status);
