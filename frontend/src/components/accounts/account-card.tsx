"use client";

import { disconnectAccount, setDefaultAccount, startConnect, testAccount } from "@/lib/api/accounts";
import { accountStateCopy, providerLabels, stateReasonCopy } from "@/lib/accounts-copy";
import { queryKeys } from "@/lib/query/query-keys";
import type { Account } from "@/lib/schemas/accounts";
import { formatDate } from "@/lib/format";
import { ProviderLogo } from "@/components/accounts/provider-logo";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Check, Star, X } from "lucide-react";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";

function Capability({ on, children }: { on: boolean; children: ReactNode }) {
  return (
    <span
      className={`inline-flex h-6 items-center gap-1 rounded-full px-2.5 text-xs font-semibold ${
        on ? "bg-muted" : "text-muted-foreground line-through"
      }`}
    >
      {on ? <Check className="size-3" strokeWidth={3} /> : <X className="size-3" />}
      {children}
    </span>
  );
}

export function AccountCard({ account }: { account: Account }) {
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts });

  const makeDefault = useMutation({
    mutationFn: () => setDefaultAccount(account.id),
    onSuccess: async () => {
      await invalidate();
      toast.success("Default sending account updated");
    },
    onError: () => toast.error("Could not set default account"),
  });

  const test = useMutation({
    mutationFn: () => testAccount(account.id),
    onSuccess: async (result) => {
      if (result.ok) {
        toast.success(`Connection works — ${result.email}`);
      } else if (result.errorCode === "account.needs_reconnect") {
        toast.error("This account needs to be reconnected");
        await invalidate();
      } else {
        toast.error("The provider is temporarily unavailable");
      }
    },
    onError: () => toast.error("Could not test the connection"),
  });

  const disconnect = useMutation({
    mutationFn: () => disconnectAccount(account.id),
    onSuccess: async () => {
      setConfirmOpen(false);
      await invalidate();
      toast.success("Account disconnected");
    },
    onError: () => toast.error("Could not disconnect the account"),
  });

  const reconnect = useMutation({
    mutationFn: () => startConnect(account.provider),
    onSuccess: (url) => window.location.assign(url),
    onError: () => toast.error("Could not start reconnect"),
  });

  const stateInfo = accountStateCopy[account.state];
  const isActive = account.state === "active";

  return (
    <Card className="h-full gap-4 px-5">
      <div className="flex items-start gap-3">
        <ProviderLogo provider={account.provider} />
        <div className="min-w-0 flex-1">
          <p className="truncate text-[15px] font-bold dark:font-semibold">{account.emailAddress}</p>
          <p className="truncate text-xs text-muted-foreground">
            {providerLabels[account.provider]}
            {account.displayName ? ` · ${account.displayName}` : ""}
          </p>
        </div>
        <Badge variant={stateInfo.variant}>{stateInfo.label}</Badge>
      </div>

      <div className="flex flex-wrap gap-1.5">
        <Capability on={isActive}>Send</Capability>
        <Capability on={isActive && account.canReadInbox}>Read inbox</Capability>
        {account.isDefault && (
          <Capability on>
            <Star className="size-3 fill-current" /> Default sender
          </Capability>
        )}
      </div>

      {isActive && !account.canReadInbox && (
        <p className="text-xs text-muted-foreground">
          Sending only — reconnect to also read this inbox and reply with templates.
        </p>
      )}
      {account.state === "needs_reconnect" && (
        <p className="text-xs font-medium text-warning">
          {(account.stateReason && stateReasonCopy[account.stateReason]) ??
            "This account needs to be reconnected."}{" "}
          Sends from it are paused until you sign in again.
        </p>
      )}

      <p className="font-mono text-[11px] text-muted-foreground">
        Connected {formatDate(account.connectedAt)} ·{" "}
        {account.lastUsedAt ? `last used ${formatDate(account.lastUsedAt)}` : "not used yet"}
      </p>

      <div className="mt-auto flex flex-wrap items-center gap-2">
        {account.state === "needs_reconnect" && (
          <Button size="sm" onClick={() => reconnect.mutate()} disabled={reconnect.isPending}>
            {reconnect.isPending ? "Redirecting…" : "Reconnect"}
          </Button>
        )}
        {isActive && !account.isDefault && (
          <Button size="sm" variant="outline" onClick={() => makeDefault.mutate()} disabled={makeDefault.isPending}>
            Make default
          </Button>
        )}
        {account.state !== "revoked" && (
          <Button size="sm" variant="outline" onClick={() => test.mutate()} disabled={test.isPending}>
            {test.isPending ? "Testing…" : "Test connection"}
          </Button>
        )}
        <span className="flex-1" />
        <Button
          size="sm"
          variant="ghost"
          className="text-muted-foreground hover:text-destructive"
          onClick={() => setConfirmOpen(true)}
        >
          Disconnect
        </Button>
      </div>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Disconnect {providerLabels[account.provider]}?</DialogTitle>
            <DialogDescription>
              {account.emailAddress} will be unlinked and any queued sends from it will be
              cancelled. You can reconnect at any time.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button variant="outline">Cancel</Button>
            </DialogClose>
            <Button
              variant="destructive"
              onClick={() => disconnect.mutate()}
              disabled={disconnect.isPending}
            >
              {disconnect.isPending ? "Disconnecting…" : "Disconnect"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
