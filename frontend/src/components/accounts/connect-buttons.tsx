"use client";

import { startConnect } from "@/lib/api/accounts";
import { ApiError } from "@/lib/api/client";
import { oauthErrorMessage, providerLabels } from "@/lib/accounts-copy";
import type { Provider } from "@/lib/schemas/accounts";
import { ProviderLogo } from "@/components/accounts/provider-logo";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

const PROVIDERS: { provider: Provider; detail: string }[] = [
  { provider: "gmail", detail: "Gmail / Google Workspace" },
  { provider: "outlook", detail: "Outlook / Microsoft 365" },
];

export function ConnectButtons({
  variant = "outline",
  stacked = false,
}: {
  variant?: "default" | "outline";
  /** Full-width rows instead of an inline wrap. */
  stacked?: boolean;
}) {
  const [pending, setPending] = useState<Provider | null>(null);

  const connect = useMutation({
    mutationFn: (provider: Provider) => startConnect(provider),
    onMutate: (provider) => setPending(provider),
    onSuccess: (authorizationUrl) => {
      // Full-page navigation to the provider consent screen.
      window.location.assign(authorizationUrl);
    },
    onError: (error) => {
      setPending(null);
      const code = error instanceof ApiError ? error.errorCode : "";
      toast.error(oauthErrorMessage(code));
    },
  });

  return (
    <div className={cn("flex gap-3", stacked ? "w-full flex-col" : "flex-wrap")}>
      {PROVIDERS.map(({ provider, detail }) => (
        <Button
          key={provider}
          variant={variant}
          size="lg"
          disabled={connect.isPending}
          onClick={() => connect.mutate(provider)}
          className={cn("h-10 gap-2.5", stacked && "justify-start")}
        >
          <ProviderLogo provider={provider} className="size-6 rounded-md border text-xs" />
          {pending === provider ? "Redirecting…" : stacked ? detail : `Connect ${providerLabels[provider]}`}
        </Button>
      ))}
    </div>
  );
}
