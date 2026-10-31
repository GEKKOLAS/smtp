export const queryKeys = {
  me: ["me"] as const,
  sessions: ["sessions"] as const,
  accounts: ["accounts"] as const,
  assets: (params?: { kind?: string; search?: string }) =>
    ["assets", params?.kind ?? "all", params?.search ?? ""] as const,
  templates: (params?: { search?: string; archived?: boolean }) =>
    ["templates", params?.search ?? "", params?.archived ?? false] as const,
  template: (id: string) => ["template", id] as const,
  templateVersions: (id: string) => ["template-versions", id] as const,
  brandKits: ["brand-kits"] as const,
  brandFonts: ["brand-fonts"] as const,
  analytics: (days: number) => ["analytics", days] as const,
  sendAnalytics: (id: string) => ["send-analytics", id] as const,
  sends: (status?: string) => ["sends", status ?? "all"] as const,
  send: (id: string) => ["send", id] as const,
  inbox: (accountId: string, search?: string) => ["inbox", accountId, search ?? ""] as const,
  inboxMessage: (accountId: string, messageId: string) =>
    ["inbox-message", accountId, messageId] as const,
};
