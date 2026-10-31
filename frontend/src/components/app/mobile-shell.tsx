"use client";

import { listAccounts } from "@/lib/api/accounts";
import { listInbox } from "@/lib/api/inbox";
import { useIsDesktop } from "@/lib/hooks/use-media-query";
import { queryKeys } from "@/lib/query/query-keys";
import type { User } from "@/lib/schemas/auth";
import { STATE_DOT, useSignOut } from "@/components/app/app-nav";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  BarChart3,
  ChevronDown,
  Image as ImageIcon,
  Inbox,
  LayoutDashboard,
  LayoutTemplate,
  LogOut,
  Palette,
  PenSquare,
  Plug,
  ScrollText,
  Settings,
  type LucideIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

// The phone shell (below `lg`): a slim top bar and three thumb-reachable tabs.
// Letters come first because on a phone you mostly answer mail; everything
// else lives in the account menu, and building templates stays on desktop.

const TABS: { href: string; label: string; icon: LucideIcon }[] = [
  { href: "/inbox", label: "Letters", icon: Inbox },
  { href: "/templates", label: "Templates", icon: LayoutTemplate },
  { href: "/sends", label: "Activity", icon: Activity },
];

const MENU: { href: string; label: string; icon: LucideIcon }[] = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/analytics", label: "Analytics", icon: BarChart3 },
  { href: "/assets", label: "Assets", icon: ImageIcon },
  { href: "/brand", label: "Brand", icon: Palette },
  { href: "/accounts", label: "Accounts", icon: Plug },
  { href: "/audit", label: "Audit log", icon: ScrollText },
  { href: "/settings", label: "Settings", icon: Settings },
];

const isActive = (pathname: string, href: string) => pathname === href || pathname.startsWith(`${href}/`);

export function MobileTopBar({ user }: { user: User }) {
  const pathname = usePathname();
  const signOut = useSignOut();
  const { data: accounts } = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const primary = accounts?.find((a) => a.isDefault) ?? accounts?.[0];

  return (
    <header className="sticky top-0 z-30 flex items-center justify-between gap-3 bg-background/85 px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-2 backdrop-blur-xl lg:hidden">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            className="flex h-9 min-w-0 items-center gap-2 rounded-full border-2 bg-card px-3.5 text-[13px] font-semibold dark:border"
          >
            <span
              className={`size-2 shrink-0 rounded-full ${primary ? STATE_DOT[primary.state] : "bg-muted-foreground"}`}
              aria-hidden
            />
            <span className="truncate">{primary?.emailAddress ?? user.displayName}</span>
            <ChevronDown className="size-3.5 shrink-0 text-muted-foreground" />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start" className="w-64">
          {accounts && accounts.length > 0 && (
            <>
              <DropdownMenuLabel>Sending from</DropdownMenuLabel>
              {accounts.map((account) => (
                <DropdownMenuItem key={account.id} asChild>
                  <Link href="/accounts">
                    <span className={`size-2 shrink-0 rounded-full ${STATE_DOT[account.state]}`} aria-hidden />
                    <span className="min-w-0 flex-1 truncate">{account.emailAddress}</span>
                    {account.isDefault && <span className="text-xs text-muted-foreground">Default</span>}
                  </Link>
                </DropdownMenuItem>
              ))}
              <DropdownMenuSeparator />
            </>
          )}
          {MENU.map((item) => (
            <DropdownMenuItem key={item.href} asChild>
              <Link href={item.href} aria-current={isActive(pathname, item.href) ? "page" : undefined}>
                <item.icon />
                {item.label}
              </Link>
            </DropdownMenuItem>
          ))}
          <DropdownMenuSeparator />
          <DropdownMenuLabel className="truncate font-normal text-muted-foreground">{user.email}</DropdownMenuLabel>
          <DropdownMenuItem onSelect={() => signOut.mutate()} disabled={signOut.isPending}>
            <LogOut />
            Sign out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <div className="flex shrink-0 items-center gap-2">
        <ThemeToggle className="size-9" />
        {pathname !== "/compose" && (
          <Button asChild variant="outline" size="icon" className="size-9" aria-label="Compose">
            <Link href="/compose">
              <PenSquare />
            </Link>
          </Button>
        )}
      </div>
    </header>
  );
}

export function MobileTabBar() {
  const pathname = usePathname();
  const isDesktop = useIsDesktop();

  // Same query (and cache entry) the Letters screen opens with, so the badge
  // costs nothing extra once you've been there. Desktop never shows the bar.
  const { data: accounts } = useQuery({ queryKey: queryKeys.accounts, queryFn: listAccounts });
  const mailboxId = accounts?.find((a) => a.canReadInbox)?.id ?? "";
  const { data: inbox } = useQuery({
    queryKey: queryKeys.inbox(mailboxId, ""),
    queryFn: () => listInbox({ accountId: mailboxId }),
    enabled: !isDesktop && mailboxId.length > 0,
  });
  const unread = inbox?.items.filter((m) => !m.isRead).length ?? 0;

  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-40 flex justify-around border-t-2 bg-card px-2 pt-2 pb-[max(0.625rem,env(safe-area-inset-bottom))] lg:hidden dark:border-t dark:bg-[#0e0e14]/95 dark:backdrop-blur-xl"
    >
      {TABS.map((tab) => {
        const active = isActive(pathname, tab.href);
        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? "page" : undefined}
            className={`relative flex w-20 flex-col items-center gap-1 text-[11px] font-semibold ${
              active ? "text-foreground" : "text-muted-foreground"
            }`}
          >
            <span
              className={`flex h-8 w-14 items-center justify-center rounded-full transition-colors ${
                active ? "bg-primary text-primary-foreground dark:bg-muted dark:text-link" : ""
              }`}
            >
              <tab.icon className="size-5" strokeWidth={2.25} />
            </span>
            {tab.href === "/inbox" && unread > 0 && (
              <span className="absolute -top-1 left-[calc(50%+0.625rem)] flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-[#ff7a45] px-1.5 font-mono text-[10px] font-bold text-[#111111] dark:bg-primary">
                {unread}
                <span className="sr-only"> unread</span>
              </span>
            )}
            {tab.label}
          </Link>
        );
      })}
    </nav>
  );
}
