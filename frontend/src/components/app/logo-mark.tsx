import { Mail } from "lucide-react";
import { cn } from "@/lib/utils";

export function LogoMark({ withWordmark = true, className }: { withWordmark?: boolean; className?: string }) {
  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      <span className="flex size-8 shrink-0 items-center justify-center rounded-xl bg-linear-to-br from-brand to-brand-2 text-white shadow-[0_0_0_1px_rgba(255,255,255,0.08),0_8px_20px_-8px_var(--brand)]">
        <Mail className="size-4" strokeWidth={2.25} />
      </span>
      {withWordmark && (
        <span className="text-base font-semibold tracking-tight text-foreground">Mail Template Hub</span>
      )}
    </div>
  );
}
