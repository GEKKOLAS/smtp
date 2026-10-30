import { cn } from "@/lib/utils";

// Light: a tilted postage stamp with a perforated edge. Dark: a soft round avatar.
const TONES = [
  "bg-[#c9bdff] dark:bg-[#2a2440] dark:text-[#d4c8ff]",
  "bg-[#9fd8ff] dark:bg-[#1e3047] dark:text-[#bfdbfe]",
  "bg-[#ffd23f] dark:bg-[#23232f] dark:text-[#b4b4c6]",
  "bg-[#ff9ed2] dark:bg-[#3a2a1e] dark:text-[#fcd9b6]",
];

export function initialsOf(name: string) {
  const parts = name.replace(/@.*/, "").split(/[\s._-]+/).filter(Boolean);
  return ((parts[0]?.[0] ?? "") + (parts[1]?.[0] ?? "")).toUpperCase() || "?";
}

/** Same sender, same color — a cheap stable hash keeps the list from shuffling hues on refetch. */
export function toneFor(name: string) {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  return TONES[Math.abs(hash) % TONES.length];
}

export function StampAvatar({ name, className }: { name: string; className?: string }) {
  return (
    <span
      aria-hidden
      className={cn(
        "flex size-8 shrink-0 -rotate-3 items-center justify-center rounded-[3px] border-2 border-dashed border-[#111111] text-[11px] font-bold text-[#111111] dark:rotate-0 dark:rounded-full dark:border-0",
        toneFor(name),
        className,
      )}
    >
      {initialsOf(name)}
    </span>
  );
}
