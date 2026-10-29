import type { SendStatus } from "@/lib/schemas/sends";
import { Badge } from "@/components/ui/badge";

type BadgeVariant = "success" | "warning" | "info" | "destructive" | "outline";

const COPY: Record<SendStatus, { label: string; variant: BadgeVariant }> = {
  scheduled: { label: "Scheduled", variant: "outline" },
  queued: { label: "Queued", variant: "info" },
  sending: { label: "Sending", variant: "info" },
  retrying: { label: "Retrying", variant: "warning" },
  sent: { label: "Sent", variant: "success" },
  partiallyfailed: { label: "Partially failed", variant: "warning" },
  failed: { label: "Failed", variant: "destructive" },
  cancelled: { label: "Cancelled", variant: "outline" },
};

export function SendStatusBadge({ status }: { status: SendStatus }) {
  const copy = COPY[status];
  return <Badge variant={copy.variant}>{copy.label}</Badge>;
}

const ACTIVE: SendStatus[] = ["queued", "sending", "retrying", "scheduled"];
export const isActiveStatus = (status: SendStatus) => ACTIVE.includes(status);
