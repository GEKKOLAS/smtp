"use client";

import { listAccounts } from "@/lib/api/accounts";
import { testSend } from "@/lib/api/sends";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

/**
 * Sends a test using the current template version. The editor auto-saves before
 * opening so the test always references a persisted version.
 */
export function TestSendDialog({
  versionId,
  disabled,
}: {
  versionId: string | undefined;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [accountId, setAccountId] = useState("");
  const accounts = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts, enabled: open });
  const active = accounts.data?.filter((a) => a.state === "active") ?? [];
  const effective = accountId || active[0]?.id || "";

  const send = useMutation({
    mutationFn: () =>
      testSend({ connectedEmailAccountId: effective, templateVersionId: versionId!, toSelf: "account" }),
    onSuccess: () => {
      setOpen(false);
      toast.success("Test email sent to your connected address");
    },
    onError: (e) =>
      toast.error(e instanceof ApiError ? e.message ?? "Test send failed" : "Test send failed"),
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm" disabled={disabled}>Test send</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Send a test</DialogTitle>
          <DialogDescription>
            Delivers this template to your connected account, with a [TEST] subject prefix.
          </DialogDescription>
        </DialogHeader>

        {active.length === 0 ? (
          <p className="text-sm text-muted-foreground">Connect an account first to send a test.</p>
        ) : (
          <div className="space-y-2">
            <Label htmlFor="test-account">From account</Label>
            <select
              id="test-account"
              value={effective}
              onChange={(e) => setAccountId(e.target.value)}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm"
            >
              {active.map((a) => (
                <option key={a.id} value={a.id}>{a.emailAddress}</option>
              ))}
            </select>
          </div>
        )}

        <DialogFooter>
          <Button
            onClick={() => send.mutate()}
            disabled={active.length === 0 || !versionId || send.isPending}
          >
            {send.isPending ? "Sending…" : "Send test"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
