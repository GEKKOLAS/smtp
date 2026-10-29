import type { Provider } from "@/lib/schemas/accounts";
import { cn } from "@/lib/utils";

// Lettermarks in each provider's color — enough to tell them apart without shipping brand logos.
const MARKS: Record<Provider, { letter: string; className: string }> = {
  gmail: { letter: "G", className: "bg-white text-[#ea4335]" },
  outlook: { letter: "O", className: "bg-[#0f6cbd] text-white" },
};

export function ProviderLogo({ provider, className }: { provider: Provider; className?: string }) {
  const mark = MARKS[provider];
  return (
    <span
      aria-hidden
      className={cn(
        "flex size-10 shrink-0 items-center justify-center rounded-lg border-2 border-[#111111] text-base font-bold dark:border-white/10",
        mark.className,
        className,
      )}
    >
      {mark.letter}
    </span>
  );
}
