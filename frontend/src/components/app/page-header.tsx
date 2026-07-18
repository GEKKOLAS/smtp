import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

export function PageHeader({
  title,
  description,
  icon: Icon,
  action,
}: {
  title: string;
  description?: string;
  icon?: LucideIcon;
  action?: ReactNode;
}) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div className="flex items-start gap-3">
        {Icon && (
          <span className="mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-xl bg-linear-to-br from-brand/15 to-brand-2/15 text-brand ring-1 ring-brand/20">
            <Icon className="size-5" />
          </span>
        )}
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-gradient-brand">{title}</h1>
          {description && <p className="text-muted-foreground">{description}</p>}
        </div>
      </div>
      {action}
    </header>
  );
}
