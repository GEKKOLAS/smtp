"use client";

import { listAccounts } from "@/lib/api/accounts";
import { logout } from "@/lib/api/auth";
import { queryKeys } from "@/lib/query/query-keys";
import type { Account } from "@/lib/schemas/accounts";
import type { User } from "@/lib/schemas/auth";
import { LogoMark } from "@/components/app/logo-mark";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Activity,
  BarChart3,
  Image as ImageIcon,
  Inbox,
  LayoutDashboard,
  LayoutTemplate,
  LogOut,
  History,
  Palette,
  PenSquare,
  Plug,
  Settings,
  type LucideIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";

type NavItem = { href: string; label: string; icon: LucideIcon };

const NAV_GROUPS: { label: string; items: NavItem[] }[] = [
  {
    label: "Create",
    items: [
      { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
      { href: "/templates", label: "Templates", icon: LayoutTemplate },
      { href: "/assets", label: "Assets", icon: ImageIcon },
      { href: "/brand", label: "Brand", icon: Palette },
      { href: "/compose", label: "Compose", icon: PenSquare },
    ],
  },
  {
    label: "Mail",
    items: [
      { href: "/inbox", label: "Inbox", icon: Inbox },
      { href: "/sends", label: "History", icon: History },
      { href: "/analytics", label: "Analytics", icon: BarChart3 },
    ],
  },
  {
    label: "Workspace",
    items: [
      { href: "/accounts", label: "Accounts", icon: Plug },
      { href: "/audit", label: "Activity", icon: Activity },
      { href: "/settings", label: "Settings", icon: Settings },
    ],
  },
];

export const NAV_ITEMS: NavItem[] = NAV_GROUPS.flatMap((group) => group.items);

export const STATE_DOT: Record<Account["state"], string> = {
  active: "bg-[#4ade80]",
  needs_reconnect: "bg-[#fbbf24]",
  revoked: "bg-[#f87171]",
};

function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? "") + (parts[1]?.[0] ?? "")).toUpperCase() || "U";
}

export function useSignOut() {
  const router = useRouter();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: logout,
    onSettled: async () => {
      queryClient.setQueryData(queryKeys.me, null);
      await queryClient.invalidateQueries();
      router.replace("/login");
    },
  });
}

export function AppNav({ user }: { user: User }) {
  const pathname = usePathname();
  const { data: accounts } = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const logoutMutation = useSignOut();

  return (
    <aside className="hidden w-64 shrink-0 flex-col gap-5 border-r border-sidebar-border bg-sidebar px-3 py-4 text-sidebar-foreground lg:flex">
      <Link href="/dashboard" className="rounded-xl px-2 py-1.5">
        <LogoMark />
      </Link>

      <nav className="flex flex-1 flex-col gap-5" aria-label="Primary">
        {NAV_GROUPS.map((group) => (
          <div key={group.label} className="flex flex-col gap-0.5">
            <p className="mb-1 px-3 font-mono text-[11px] font-bold tracking-[0.06em] text-sidebar-foreground/55 uppercase dark:font-sans dark:font-medium">
              {group.label}
            </p>
            {group.items.map((item) => {
              const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
              const Icon = item.icon;
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={`flex h-10 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors dark:h-9 ${
                    active
                      ? "bg-sidebar-active font-bold text-sidebar-active-foreground dark:font-semibold"
                      : "font-medium text-sidebar-foreground/65 hover:bg-sidebar-accent hover:text-sidebar-foreground"
                  }`}
                >
                  <Icon className={`size-4 shrink-0 ${active ? "text-sidebar-icon" : ""}`} strokeWidth={2.25} />
                  {item.label}
                </Link>
              );
            })}
          </div>
        ))}
      </nav>

      {accounts && accounts.length > 0 && (
        <Link
          href="/accounts"
          className="flex flex-col gap-2.5 rounded-xl border border-sidebar-border bg-sidebar-accent/60 p-3 transition-colors hover:bg-sidebar-accent"
        >
          <span className="font-mono text-[11px] font-bold tracking-[0.06em] text-sidebar-foreground/55 uppercase dark:font-sans dark:font-medium">
            Sending from
          </span>
          {accounts.slice(0, 3).map((account) => (
            <span key={account.id} className="flex items-center gap-2 text-[13px]">
              <span className={`size-2 shrink-0 rounded-full ${STATE_DOT[account.state]}`} aria-hidden />
              <span className="min-w-0 flex-1 truncate">{account.emailAddress}</span>
              {account.isDefault && <span className="text-[11px] text-sidebar-foreground/55">Default</span>}
              {account.state !== "active" && <span className="sr-only">({account.state.replace("_", " ")})</span>}
            </span>
          ))}
        </Link>
      )}

      <div className="flex items-center gap-2.5 border-t border-sidebar-border px-1 pt-3">
        <Avatar size="sm">
          <AvatarFallback className="-rotate-3 rounded-[3px] border-2 border-dashed border-[#111111] bg-[#ff9ed2] font-bold text-[#111111] dark:rotate-0 dark:rounded-full dark:border-0 dark:bg-linear-to-br dark:from-brand dark:to-brand-2 dark:text-white">
            {initials(user.displayName)}
          </AvatarFallback>
        </Avatar>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{user.displayName}</p>
          <p className="truncate text-xs text-sidebar-foreground/60">{user.email}</p>
        </div>
        <button
          type="button"
          aria-label="Sign out"
          title="Sign out"
          onClick={() => logoutMutation.mutate()}
          disabled={logoutMutation.isPending}
          className="flex size-8 shrink-0 items-center justify-center rounded-lg text-sidebar-foreground/60 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground disabled:opacity-50"
        >
          <LogOut className="size-4" />
        </button>
      </div>
    </aside>
  );
}
