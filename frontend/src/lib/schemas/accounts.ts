import { z } from "zod";

export const accountStateSchema = z.enum(["active", "needs_reconnect", "revoked"]);
export type AccountState = z.infer<typeof accountStateSchema>;

export const providerSchema = z.enum(["gmail", "outlook"]);
export type Provider = z.infer<typeof providerSchema>;

export const accountSchema = z.object({
  id: z.string(),
  provider: providerSchema,
  emailAddress: z.string(),
  displayName: z.string().nullable(),
  state: accountStateSchema,
  stateReason: z.string().nullable(),
  isDefault: z.boolean(),
  grantedScopes: z.array(z.string()),
  connectedAt: z.string(),
  lastUsedAt: z.string().nullable(),
});
export type Account = z.infer<typeof accountSchema>;

export const accountTestResultSchema = z.object({
  ok: z.boolean(),
  email: z.string().nullable(),
  displayName: z.string().nullable(),
  errorCode: z.string().nullable(),
});
export type AccountTestResult = z.infer<typeof accountTestResultSchema>;
