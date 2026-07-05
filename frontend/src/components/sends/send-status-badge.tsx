import type { SendStatus } from "@/lib/schemas/sends";
import { Badge } from "@/components/ui/badge";

const COPY: Record<SendStatus, { label: string; variant: "default" | "secondary" | "destructive" | "outline" }> = {
  scheduled: { label: "Scheduled", variant: "outline" },
  queued: { label: "Queued", variant: "secondary" },
  sending: { label: "Sending", variant: "secondary" },
  retrying: { label: "Retrying", variant: "secondary" },
  sent: { label: "Sent", variant: "default" },
  partiallyfailed: { label: "Partially failed", variant: "destructive" },
  failed: { label: "Failed", variant: "destructive" },
  cancelled: { label: "Cancelled", variant: "outline" },
};

export function SendStatusBadge({ status }: { status: SendStatus }) {
  const copy = COPY[status];
  return <Badge variant={copy.variant}>{copy.label}</Badge>;
}

const ACTIVE: SendStatus[] = ["queued", "sending", "retrying", "scheduled"];
export const isActiveStatus = (status: SendStatus) => ACTIVE.includes(status);
