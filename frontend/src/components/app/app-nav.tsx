"use client";

import { logout } from "@/lib/api/auth";
import { queryKeys } from "@/lib/query/query-keys";
import type { User } from "@/lib/schemas/auth";
import { Button } from "@/components/ui/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";

const NAV_ITEMS = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/accounts", label: "Accounts" },
  { href: "/templates", label: "Templates" },
  { href: "/assets", label: "Assets" },
  { href: "/compose", label: "Compose" },
  { href: "/sends", label: "History" },
  { href: "/settings", label: "Settings" },
];

export function AppNav({ user }: { user: User }) {
  const pathname = usePathname();
  const router = useRouter();
  const queryClient = useQueryClient();

  const logoutMutation = useMutation({
    mutationFn: logout,
    onSettled: async () => {
      queryClient.setQueryData(queryKeys.me, null);
      await queryClient.invalidateQueries();
      router.replace("/login");
    },
  });

  return (
    <aside className="flex w-60 shrink-0 flex-col border-r bg-muted/20">
      <div className="px-4 py-5">
        <span className="text-lg font-semibold tracking-tight">Mail Template Hub</span>
      </div>
      <nav className="flex-1 space-y-1 px-2" aria-label="Primary">
        {NAV_ITEMS.map((item) => {
          const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={active ? "page" : undefined}
              className={`block rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                active
                  ? "bg-background text-foreground shadow-sm"
                  : "text-muted-foreground hover:bg-background/60 hover:text-foreground"
              }`}
            >
              {item.label}
            </Link>
          );
        })}
      </nav>
      <div className="border-t p-4">
        <p className="truncate text-sm font-medium">{user.displayName}</p>
        <p className="mb-3 truncate text-xs text-muted-foreground">{user.email}</p>
        <Button
          variant="outline"
          size="sm"
          className="w-full"
          onClick={() => logoutMutation.mutate()}
          disabled={logoutMutation.isPending}
        >
          {logoutMutation.isPending ? "Signing out…" : "Sign out"}
        </Button>
      </div>
    </aside>
  );
}
