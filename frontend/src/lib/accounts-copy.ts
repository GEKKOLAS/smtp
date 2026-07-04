import type { AccountState, Provider } from "@/lib/schemas/accounts";

export const providerLabels: Record<Provider, string> = {
  gmail: "Gmail",
  outlook: "Outlook",
};

/** Human copy for the ?error= codes the OAuth callback can redirect with. */
export const oauthErrorMessages: Record<string, string> = {
  "oauth.access_denied": "You cancelled the connection at the provider.",
  "oauth.scope_missing":
    "The connection was made, but sending permission was not granted. Reconnect and allow sending.",
  "oauth.state_invalid": "The connection request expired or was invalid. Please try again.",
  "oauth.exchange_failed": "We couldn't complete the connection. Please try again.",
  "oauth.account_limit": "You've reached the maximum number of connected accounts.",
};

export function oauthErrorMessage(code: string): string {
  return oauthErrorMessages[code] ?? "The connection could not be completed. Please try again.";
}

export const accountStateCopy: Record<AccountState, { label: string; variant: "default" | "secondary" | "destructive" }> = {
  active: { label: "Active", variant: "default" },
  needs_reconnect: { label: "Needs reconnect", variant: "destructive" },
  revoked: { label: "Disconnected", variant: "secondary" },
};

export const stateReasonCopy: Record<string, string> = {
  insufficient_scope: "Sending permission wasn't granted.",
  invalid_grant: "Access was revoked at the provider.",
  user_disconnect: "You disconnected this account.",
};
