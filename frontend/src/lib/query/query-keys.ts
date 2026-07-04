export const queryKeys = {
  me: ["me"] as const,
  sessions: ["sessions"] as const,
  accounts: ["accounts"] as const,
  assets: (params?: { kind?: string; search?: string }) =>
    ["assets", params?.kind ?? "all", params?.search ?? ""] as const,
};
