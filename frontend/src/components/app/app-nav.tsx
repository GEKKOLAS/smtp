"use client";

import { logout } from "@/lib/api/auth";
import { queryKeys } from "@/lib/query/query-keys";
import type { User } from "@/lib/schemas/auth";
import { LogoMark } from "@/components/app/logo-mark";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Activity,
  Image as ImageIcon,
  LayoutDashboard,
  LayoutTemplate,
  LogOut,
  History,
  PenSquare,
  Plug,
  Settings,
  type LucideIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";

export const NAV_ITEMS: { href: string; label: string; icon: LucideIcon }[] = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/accounts", label: "Accounts", icon: Plug },
  { href: "/templates", label: "Templates", icon: LayoutTemplate },
  { href: "/assets", label: "Assets", icon: ImageIcon },
  { href: "/compose", label: "Compose", icon: PenSquare },
  { href: "/sends", label: "History", icon: History },
  { href: "/audit", label: "Activity", icon: Activity },
  { href: "/settings", label: "Settings", icon: Settings },
];

function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? "") + (parts[1]?.[0] ?? "")).toUpperCase() || "U";
}

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
    <aside className="flex w-64 shrink-0 flex-col border-r border-sidebar-border bg-sidebar/80 backdrop-blur-xl">
      <div className="px-5 py-5">
        <LogoMark />
      </div>
      <nav className="flex-1 space-y-1 px-3" aria-label="Primary">
        {NAV_ITEMS.map((item) => {
          const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          const Icon = item.icon;
          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={active ? "page" : undefined}
              className={`flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-medium transition-all ${
                active
                  ? "bg-linear-to-r from-brand/15 to-brand-2/15 text-brand ring-1 ring-brand/25 shadow-[0_0_0_1px_rgba(255,255,255,0.03)_inset]"
                  : "text-sidebar-foreground/65 hover:bg-sidebar-accent hover:text-sidebar-foreground"
              }`}
            >
              <Icon className="size-4 shrink-0" strokeWidth={2.25} />
              {item.label}
            </Link>
          );
        })}
      </nav>
      <div className="border-t border-sidebar-border p-3">
        <div className="flex items-center gap-2.5 rounded-xl px-2 py-2">
          <Avatar size="sm">
            <AvatarFallback className="bg-linear-to-br from-brand to-brand-2 text-white">
              {initials(user.displayName)}
            </AvatarFallback>
          </Avatar>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium text-sidebar-foreground">{user.displayName}</p>
            <p className="truncate text-xs text-sidebar-foreground/60">{user.email}</p>
          </div>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="mt-2 w-full"
          onClick={() => logoutMutation.mutate()}
          disabled={logoutMutation.isPending}
        >
          <LogOut className="size-3.5" />
          {logoutMutation.isPending ? "Signing out…" : "Sign out"}
        </Button>
      </div>
    </aside>
  );
}
