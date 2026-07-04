"use client";

import { disconnectAccount, setDefaultAccount, startConnect, testAccount } from "@/lib/api/accounts";
import { accountStateCopy, providerLabels, stateReasonCopy } from "@/lib/accounts-copy";
import { queryKeys } from "@/lib/query/query-keys";
import type { Account } from "@/lib/schemas/accounts";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { MoreVertical, Star } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

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

  return (
    <Card>
      <CardContent className="flex items-center justify-between gap-4 py-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-medium">{providerLabels[account.provider]}</span>
            {account.isDefault && (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                <Star className="size-3 fill-current" /> Default
              </span>
            )}
          </div>
          <p className="truncate text-sm text-muted-foreground">{account.emailAddress}</p>
          {account.state === "needs_reconnect" && account.stateReason && (
            <p className="mt-1 text-xs text-destructive">
              {stateReasonCopy[account.stateReason] ?? "This account needs to be reconnected."}
            </p>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <Badge variant={stateInfo.variant}>{stateInfo.label}</Badge>

          {account.state === "needs_reconnect" ? (
            <Button size="sm" onClick={() => reconnect.mutate()} disabled={reconnect.isPending}>
              Reconnect
            </Button>
          ) : (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Account actions">
                  <MoreVertical className="size-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {!account.isDefault && account.state === "active" && (
                  <DropdownMenuItem onSelect={() => makeDefault.mutate()}>
                    Make default
                  </DropdownMenuItem>
                )}
                <DropdownMenuItem onSelect={() => test.mutate()} disabled={test.isPending}>
                  Test connection
                </DropdownMenuItem>
                <DropdownMenuItem
                  variant="destructive"
                  onSelect={(e) => {
                    e.preventDefault();
                    setConfirmOpen(true);
                  }}
                >
                  Disconnect
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </CardContent>

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
