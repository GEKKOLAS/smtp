"use client";

import { AppNav, NAV_ITEMS } from "@/components/app/app-nav";
import { GradientBackdrop } from "@/components/app/gradient-backdrop";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { useSession } from "@/lib/hooks/use-session";
import { useRouter, usePathname } from "next/navigation";
import { useEffect } from "react";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { data: user, isLoading } = useSession();

  useEffect(() => {
    if (!isLoading && user === null) {
      const next = encodeURIComponent(window.location.pathname);
      router.replace(`/login?next=${next}`);
    }
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return (
      <div className="flex min-h-full flex-1 items-center justify-center">
        <p className="text-sm text-muted-foreground">Loading…</p>
      </div>
    );
  }

  const activeLabel = NAV_ITEMS.find(
    (item) => pathname === item.href || pathname.startsWith(`${item.href}/`),
  )?.label;

  return (
    <div className="relative flex min-h-full flex-1">
      <GradientBackdrop />
      <AppNav user={user} />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-border/70 bg-background/60 px-8 backdrop-blur-xl">
          <p className="text-sm font-medium text-muted-foreground">{activeLabel}</p>
          <ThemeToggle />
        </header>
        <main className="flex-1 overflow-y-auto px-8 py-8">{children}</main>
      </div>
    </div>
  );
}
