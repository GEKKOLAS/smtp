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
};
