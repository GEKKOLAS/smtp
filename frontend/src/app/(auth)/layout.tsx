import { GradientBackdrop } from "@/components/app/gradient-backdrop";
import { LogoMark } from "@/components/app/logo-mark";
import { ThemeToggle } from "@/components/theme/theme-toggle";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative flex min-h-full flex-1">
      <GradientBackdrop />

      <section
        aria-label="About Mail Template Hub"
        className="relative hidden flex-1 flex-col justify-between overflow-hidden bg-sidebar p-12 text-sidebar-foreground lg:flex"
      >
        <LogoMark />

        {/* Two postcards: what you design here is what lands in the inbox. Decorative only. */}
        <div aria-hidden className="relative h-80">
          <div className="absolute top-6 left-[6%] h-72 w-56 -rotate-[8deg] overflow-hidden rounded-xl border-2 border-white bg-[#05050f] px-4 pt-4 text-center shadow-[8px_8px_0_0_var(--primary)] dark:-rotate-6 dark:border dark:border-white/10 dark:shadow-[0_30px_60px_-20px_rgba(0,0,0,0.9)]">
            <p className="text-[10px] font-bold tracking-[0.5em] text-white">ATELLIERS</p>
            <p className="mt-1 text-[6px] tracking-[0.3em] text-[#8b7cf6]">CREATIVE STUDIO</p>
            <div className="mt-3 bg-linear-to-br from-[#1d1659] to-[#0e0b33] px-2 py-6">
              <p className="text-[6px] tracking-[0.3em] text-[#22d3ee]">WEB · BRAND · DIGITAL</p>
              <p className="mt-2 font-serif text-base leading-tight font-bold text-white">Digital Design With Atmosphere</p>
            </div>
            <div className="mx-4 mt-3 bg-[#0a1028] p-3 font-serif text-[10px] font-bold text-[#22d3ee]">Art · Design · Ambience</div>
          </div>
          <div className="absolute top-0 left-[44%] h-76 w-56 rotate-6 overflow-hidden rounded-xl border-2 border-white bg-[#fff7ed] px-4 pt-5 text-center shadow-[8px_8px_0_0_var(--primary)] dark:rotate-5 dark:border dark:border-white/10 dark:shadow-[0_30px_60px_-20px_rgba(0,0,0,0.9)]">
            <div className="mx-auto size-6 rounded-full bg-[#f59e0b]" />
            <p className="mt-2 text-[9px] font-bold tracking-[0.3em] text-[#3b2314]">SUNRISE ROASTERS</p>
            <div className="mt-3 bg-[#3b2314] px-2 py-5">
              <p className="text-sm font-bold text-[#fff7ed]">Launch week is here</p>
              <div className="mx-auto mt-2.5 h-4 w-16 bg-[#f59e0b]" />
            </div>
            <div className="mt-3 flex flex-col items-center gap-1.5">
              <div className="h-1 w-44 rounded bg-[#d6c3ae]" />
              <div className="h-1 w-40 rounded bg-[#d6c3ae]" />
              <div className="h-1 w-28 rounded bg-[#d6c3ae]" />
            </div>
          </div>
        </div>

        <div className="max-w-xl">
          <h2 className="font-heading text-5xl leading-[1.02] font-bold tracking-[-0.045em] xl:text-6xl dark:text-5xl dark:font-semibold dark:tracking-[-0.035em]">
            Design once.
            <br />
            <span className="text-sidebar-primary">Send from your own inbox.</span>
          </h2>
          <p className="mt-4 text-[15px] leading-relaxed text-sidebar-foreground/65">
            Build email templates visually or in MJML, send them through Gmail or Outlook, and answer replies without
            leaving the app.
          </p>
        </div>
      </section>

      <div className="relative flex flex-1 flex-col items-center justify-center px-4 py-12">
        <div className="absolute top-4 right-4">
          <ThemeToggle />
        </div>
        <div className="w-full max-w-sm">
          <div className="mb-6 flex flex-col items-center text-center lg:hidden">
            <LogoMark withWordmark={false} className="mb-4" />
            <p className="font-heading text-2xl font-bold tracking-tight">Mail Template Hub</p>
            <p className="text-sm text-muted-foreground">Design once, send from your own Gmail and Outlook.</p>
          </div>
          {children}
        </div>
      </div>
    </div>
  );
}
