"use client";

import { AppNav, NAV_ITEMS } from "@/components/app/app-nav";
import { GradientBackdrop } from "@/components/app/gradient-backdrop";
import { MobileTabBar, MobileTopBar } from "@/components/app/mobile-shell";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { Button } from "@/components/ui/button";
import { useSession } from "@/lib/hooks/use-session";
import { PenSquare } from "lucide-react";
import Link from "next/link";
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

  // Focus mode: the template editor brings its own header and needs every pixel of width,
  // so it renders without the sidebar and the app topbar.
  if (/^\/templates\/[^/]+\/edit\/?$/.test(pathname)) {
    return <div className="flex h-dvh min-h-0 flex-1 flex-col">{children}</div>;
  }

  const activeLabel = NAV_ITEMS.find(
    (item) => pathname === item.href || pathname.startsWith(`${item.href}/`),
  )?.label;
  const firstName = user.displayName.trim().split(/\s+/)[0] || "Your";

  return (
    <div className="relative flex min-h-full flex-1">
      <GradientBackdrop />
      <AppNav user={user} />
      <div className="flex min-w-0 flex-1 flex-col">
        <MobileTopBar user={user} />
        <header className="hidden h-14 shrink-0 items-center justify-between gap-4 border-b-2 bg-background/80 px-8 backdrop-blur-xl lg:flex dark:border-b">
          <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-2 text-sm text-muted-foreground">
            <span className="truncate">{firstName}&apos;s workspace</span>
            {activeLabel && (
              <>
                <span aria-hidden>/</span>
                <span className="font-semibold text-foreground">{activeLabel}</span>
              </>
            )}
          </nav>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            {pathname !== "/compose" && (
              <Button asChild variant="gradient">
                <Link href="/compose">
                  <PenSquare />
                  Compose
                </Link>
              </Button>
            )}
          </div>
        </header>
        {/* Bottom padding on phones clears the fixed tab bar. */}
        <main className="flex-1 overflow-y-auto px-4 pt-2 pb-28 lg:px-8 lg:py-8">{children}</main>
      </div>
      <MobileTabBar />
    </div>
  );
}
