"use client";

import { listAccounts } from "@/lib/api/accounts";
import { oauthErrorMessage, providerLabels } from "@/lib/accounts-copy";
import { queryKeys } from "@/lib/query/query-keys";
import type { Provider } from "@/lib/schemas/accounts";
import { PageHeader } from "@/components/app/page-header";
import { AccountCard } from "@/components/accounts/account-card";
import { ConnectButtons } from "@/components/accounts/connect-buttons";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useQuery } from "@tanstack/react-query";
import { Plug } from "lucide-react";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect } from "react";
import { toast } from "sonner";

export default function AccountsPage() {
  return (
    <Suspense fallback={null}>
      <AccountsInner />
    </Suspense>
  );
}

function AccountsInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { data: accounts, isLoading, isError } = useQuery({
    queryKey: queryKeys.accounts,
    queryFn: listAccounts,
  });

  // Surface the OAuth callback outcome carried in the query string, then clean the URL.
  useEffect(() => {
    const connected = searchParams.get("connected");
    const error = searchParams.get("error");
    if (!connected && !error) return;

    if (connected) {
      toast.success(`${providerLabels[connected as Provider] ?? connected} connected`);
    } else if (error) {
      toast.error(oauthErrorMessage(error));
    }
    router.replace("/accounts");
  }, [searchParams, router]);

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <PageHeader
        icon={Plug}
        title="Connected accounts"
        description="Link Gmail or Outlook to send from your own address. We request send-only access and never see your password."
      />

      <ConnectButtons />

      {isLoading && <p className="text-sm text-muted-foreground">Loading accounts…</p>}
      {isError && <p className="text-sm text-destructive">Could not load your accounts.</p>}

      {accounts && accounts.length === 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">No accounts connected yet</CardTitle>
            <CardDescription>
              Connect Gmail or Outlook above to start sending templated email.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              You can connect multiple accounts and choose a default sender.
            </p>
          </CardContent>
        </Card>
      )}

      {accounts && accounts.length > 0 && (
        <div className="space-y-3">
          {accounts.map((account) => (
            <AccountCard key={account.id} account={account} />
          ))}
        </div>
      )}
    </div>
  );
}
