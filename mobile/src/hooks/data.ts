import { isSignedOutError } from "@/api/client";
import { getMe, listAccounts, listInbox, queryKeys } from "@/api/endpoints";
import { useQuery } from "@tanstack/react-query";

/** The signed-in user, or `null` when this device has no valid session. */
export function useSession() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: async () => {
      try {
        return await getMe();
      } catch (error) {
        if (isSignedOutError(error)) return null;
        throw error;
      }
    },
    staleTime: 5 * 60_000,
  });
}

/** Connected accounts plus the mailbox the phone reads: the default one if it can read mail, else the first that can. */
export function useMailbox() {
  const accounts = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const readable = accounts.data?.filter((a) => a.canReadInbox) ?? [];
  const mailbox = readable.find((a) => a.isDefault) ?? readable[0];
  return { accounts, mailbox };
}

export function useInbox(accountId: string | undefined, search = "") {
  return useQuery({
    queryKey: queryKeys.inbox(accountId ?? "", search),
    queryFn: () => listInbox(accountId!, search),
    enabled: !!accountId,
  });
}

/** "10:31" for today, otherwise "Oct 28" — the usual mail-list format. */
export function formatMailTime(iso: string | null) {
  if (!iso) return "";
  const date = new Date(iso);
  return date.toDateString() === new Date().toDateString()
    ? date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })
    : date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}
