import { api } from "@/lib/api/client";
import { z } from "zod";

export const auditLogSchema = z.object({
  id: z.string(),
  action: z.string(),
  entityType: z.string().nullable(),
  entityId: z.string().nullable(),
  ip: z.string().nullable(),
  metadata: z.unknown().nullable(),
  createdAt: z.string(),
});
export type AuditLog = z.infer<typeof auditLogSchema>;

const pagedAuditSchema = z.object({
  items: z.array(auditLogSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export function listAuditLogs(params: { action?: string; page?: number } = {}) {
  const query = new URLSearchParams();
  if (params.action) query.set("action", params.action);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", "50");
  return api(`/audit-logs?${query.toString()}`, { schema: pagedAuditSchema });
}

// Human-friendly labels for the action codes (spec 04-security.md §7).
export const AUDIT_ACTION_LABELS: Record<string, string> = {
  "auth.register": "Account created",
  "auth.login": "Signed in",
  "auth.login_failed": "Failed sign-in",
  "auth.logout": "Signed out",
  "auth.password_reset": "Password reset",
  "auth.password_changed": "Password changed",
  "auth.session_revoked": "Session revoked",
  "account.connected": "Account connected",
  "account.disconnected": "Account disconnected",
  "account.default_changed": "Default account changed",
  "oauth.state_rejected": "OAuth request rejected",
  "token.refresh_failed": "Token refresh failed",
  "asset.uploaded": "Asset uploaded",
  "asset.deleted": "Asset deleted",
  "asset.rejected": "Upload rejected",
  "template.created": "Template created",
  "template.updated": "Template updated",
  "template.version_saved": "Template version saved",
  "template.duplicated": "Template duplicated",
  "template.archived": "Template archived",
  "template.deleted": "Template deleted",
  "template.restored": "Template restored",
  "send.created": "Send created",
  "send.scheduled": "Send scheduled",
  "send.cancelled": "Send cancelled",
  "send.retried": "Send retried",
  "send.completed": "Send completed",
  "send.failed": "Send failed",
};

export const auditLabel = (action: string) => AUDIT_ACTION_LABELS[action] ?? action;
