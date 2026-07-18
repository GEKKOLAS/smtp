import { GradientBackdrop } from "@/components/app/gradient-backdrop";
import { LogoMark } from "@/components/app/logo-mark";
import { ThemeToggle } from "@/components/theme/theme-toggle";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative flex min-h-full flex-1 flex-col items-center justify-center px-4 py-12">
      <GradientBackdrop />
      <div className="absolute top-4 right-4">
        <ThemeToggle />
      </div>
      <div className="w-full max-w-sm">
        <div className="mb-6 flex flex-col items-center text-center">
          <LogoMark withWordmark={false} className="mb-4" />
          <h1 className="text-2xl font-semibold tracking-tight">Mail Template Hub</h1>
          <p className="text-sm text-muted-foreground">
            Design once, send from your own Gmail and Outlook.
          </p>
        </div>
        {children}
      </div>
    </div>
  );
}
