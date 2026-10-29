import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
}: {
  icon?: LucideIcon;
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed px-6 py-14 text-center dark:border dark:border-input",
        className,
      )}
    >
      {Icon && (
        <span className="flex size-11 -rotate-3 items-center justify-center rounded-full border-2 bg-card shadow-hard-sm dark:rotate-0 dark:border-0 dark:bg-muted dark:text-muted-foreground dark:shadow-none">
          <Icon className="size-5" />
        </span>
      )}
      <div>
        <p className="font-semibold">{title}</p>
        {description && <p className="mt-1 max-w-md text-sm text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  );
}
