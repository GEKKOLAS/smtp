import { api } from "@/lib/api/client";
import {
  type Account,
  accountSchema,
  type AccountTestResult,
  accountTestResultSchema,
  type Provider,
} from "@/lib/schemas/accounts";
import { z } from "zod";

const listSchema = z.object({ items: z.array(accountSchema) });
const startSchema = z.object({ authorizationUrl: z.string() });

export function listAccounts(): Promise<Account[]> {
  return api("/email-accounts", { schema: listSchema }).then((r) => r.items);
}

/** Returns the provider consent URL to send the browser to. */
export function startConnect(provider: Provider, returnTo = "/accounts"): Promise<string> {
  const query = new URLSearchParams({ returnTo }).toString();
  return api(`/oauth/${provider}/start?${query}`, { schema: startSchema }).then(
    (r) => r.authorizationUrl,
  );
}

export function setDefaultAccount(id: string): Promise<void> {
  return api(`/email-accounts/${id}/default`, { method: "POST" });
}

export function testAccount(id: string): Promise<AccountTestResult> {
  return api(`/email-accounts/${id}/test`, { method: "POST", schema: accountTestResultSchema });
}

export function disconnectAccount(id: string): Promise<void> {
  return api(`/email-accounts/${id}`, { method: "DELETE" });
}
