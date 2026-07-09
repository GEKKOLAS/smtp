import { api } from "@/lib/api/client";
import { z } from "zod";

export const apiKeySchema = z.object({
  id: z.string(),
  name: z.string(),
  prefix: z.string(),
  createdAt: z.string(),
  lastUsedAt: z.string().nullable(),
  expiresAt: z.string().nullable(),
});
export type ApiKey = z.infer<typeof apiKeySchema>;

const listSchema = z.object({ items: z.array(apiKeySchema) });
const createdSchema = z.object({ key: apiKeySchema, secret: z.string() });

export function listApiKeys(): Promise<ApiKey[]> {
  return api("/api-keys", { schema: listSchema }).then((r) => r.items);
}

export function createApiKey(input: { name: string; expiresInDays?: number | null }) {
  return api("/api-keys", { body: input, schema: createdSchema });
}

export function revokeApiKey(id: string): Promise<void> {
  return api(`/api-keys/${id}`, { method: "DELETE" });
}
