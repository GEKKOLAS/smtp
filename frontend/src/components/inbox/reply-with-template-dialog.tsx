"use client";

import { ApiError } from "@/lib/api/client";
import { createSend } from "@/lib/api/sends";
import { getTemplate, listTemplates } from "@/lib/api/templates";
import { queryKeys } from "@/lib/query/query-keys";
import type { InboxMessageDetail } from "@/lib/schemas/inbox";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect } from "@/components/ui/native-select";
import { useMutation, useQuery } from "@tanstack/react-query";
import { CornerUpLeft } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

/**
 * Answers an inbox message with a template. Only the provider message id is
 * sent: the backend resolves the recipient and the thread headers from the
 * mailbox, so the reply lands in the original conversation.
 */
export function ReplyWithTemplateDialog({
  accountId,
  message,
  open,
  onOpenChange,
}: {
  accountId: string;
  message: InboxMessageDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const router = useRouter();
  const [templateId, setTemplateId] = useState("");
  const [subject, setSubject] = useState(message.replySubject);
  const [variables, setVariables] = useState<Record<string, string>>({});

  const templates = useQuery({
    queryKey: queryKeys.templates(),
    queryFn: () => listTemplates({ pageSize: 100 }),
    enabled: open,
  });

  const template = useQuery({
    queryKey: queryKeys.template(templateId),
    queryFn: () => getTemplate(templateId),
    enabled: open && templateId.length > 0,
  });

  const templateVariables = template.data?.currentVersion?.variablesSchema ?? [];
  const versionId = template.data?.currentVersion?.id;

  const send = useMutation({
    mutationFn: () =>
      createSend({
        connectedEmailAccountId: accountId,
        templateVersionId: versionId!,
        variables,
        replyToMessageId: message.id,
        subjectOverride: subject.trim() || null,
      }),
    onSuccess: (job) => {
      toast.success(`Reply queued to ${message.fromEmail}`);
      onOpenChange(false);
      router.push(`/sends/${job.id}`);
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.errorCode : "";
      toast.error(
        code === "send.variables_missing"
          ? "Fill in the template variables before replying."
          : code === "inbox.scope_missing"
            ? "Reconnect this account to allow reading mail."
            : code === "send.too_large"
              ? "The message exceeds the size limit."
              : "Could not send the reply.",
      );
    },
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <CornerUpLeft className="size-4" />
            Reply with a template
          </DialogTitle>
          <DialogDescription>
            Goes to {message.fromName ? `${message.fromName} (${message.fromEmail})` : message.fromEmail}{" "}
            in the same conversation.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="reply-template">Template</Label>
            <NativeSelect
              id="reply-template"
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
            >
              <option value="">Select a template…</option>
              {templates.data?.items.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </NativeSelect>
          </div>

          <div className="space-y-2">
            <Label htmlFor="reply-subject">Subject</Label>
            <Input
              id="reply-subject"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder={message.replySubject}
            />
          </div>

          {templateVariables.length > 0 && (
            <div className="space-y-2">
              <Label>Variables</Label>
              {templateVariables.map((v) => (
                <div key={v.name} className="flex items-center gap-2">
                  <span className="w-32 shrink-0 font-mono text-xs">{`{{${v.name}}}`}</span>
                  <Input
                    value={variables[v.name] ?? ""}
                    placeholder={v.sample ?? v.default ?? ""}
                    onChange={(e) =>
                      setVariables((prev) => ({ ...prev, [v.name]: e.target.value }))
                    }
                    className="h-8 text-sm"
                  />
                </div>
              ))}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => send.mutate()} disabled={!versionId || send.isPending}>
            {send.isPending ? "Sending…" : "Send reply"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
