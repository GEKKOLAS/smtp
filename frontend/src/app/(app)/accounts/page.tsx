"use client";

import { listAccounts } from "@/lib/api/accounts";
import { oauthErrorMessage, providerLabels } from "@/lib/accounts-copy";
import { queryKeys } from "@/lib/query/query-keys";
import type { Provider } from "@/lib/schemas/accounts";
import { PageHeader } from "@/components/app/page-header";
import { AccountCard } from "@/components/accounts/account-card";
import { ConnectButtons } from "@/components/accounts/connect-buttons";
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

  const attention = accounts?.filter((a) => a.state === "needs_reconnect").length ?? 0;
  const summary = accounts
    ? [
        `${accounts.length} ${accounts.length === 1 ? "mailbox" : "mailboxes"}`,
        attention > 0 ? `${attention} ${attention === 1 ? "needs" : "need"} attention` : accounts.length > 0 && "all healthy",
      ]
        .filter(Boolean)
        .join(" · ")
    : undefined;

  return (
    <div className="mx-auto max-w-5xl space-y-7">
      <PageHeader
        icon={Plug}
        eyebrow={summary}
        title="Accounts"
        description="Mail goes out through your own Gmail or Outlook, so replies land where you already read them. We never see your password."
      />

      {isLoading && <p className="text-sm text-muted-foreground">Loading accounts…</p>}
      {isError && <p className="text-sm text-destructive">Could not load your accounts.</p>}

      {accounts && (
        <ul className="grid gap-5 md:grid-cols-2">
          {accounts.map((account) => (
            <li
              key={account.id}
              className="odd:-rotate-[0.5deg] even:rotate-[0.4deg] dark:odd:rotate-0 dark:even:rotate-0"
            >
              <AccountCard account={account} />
            </li>
          ))}
          <li>
            <div className="flex h-full min-h-56 flex-col items-center justify-center gap-4 rounded-xl border-2 border-dashed p-6 text-center dark:border dark:border-input">
              <div>
                <p className="font-bold dark:font-semibold">
                  {accounts.length === 0 ? "Connect your first mailbox" : "Connect another mailbox"}
                </p>
                <p className="mt-1 text-sm text-muted-foreground">
                  You can connect several and pick which one sends by default.
                </p>
              </div>
              <div className="w-full max-w-72">
                <ConnectButtons stacked />
              </div>
            </div>
          </li>
        </ul>
      )}
    </div>
  );
}
