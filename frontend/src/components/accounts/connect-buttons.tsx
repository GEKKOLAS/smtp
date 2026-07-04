"use client";

import { startConnect } from "@/lib/api/accounts";
import { ApiError } from "@/lib/api/client";
import { oauthErrorMessage } from "@/lib/accounts-copy";
import type { Provider } from "@/lib/schemas/accounts";
import { Button } from "@/components/ui/button";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

export function ConnectButtons({ variant = "default" }: { variant?: "default" | "outline" }) {
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
    <div className="flex flex-wrap gap-3">
      <Button
        variant={variant}
        disabled={connect.isPending}
        onClick={() => connect.mutate("gmail")}
      >
        {pending === "gmail" ? "Redirecting…" : "Connect Gmail"}
      </Button>
      <Button
        variant={variant}
        disabled={connect.isPending}
        onClick={() => connect.mutate("outlook")}
      >
        {pending === "outlook" ? "Redirecting…" : "Connect Outlook"}
      </Button>
    </div>
  );
}
