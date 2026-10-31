"use client";

import {
  type BrandKit,
  listBrandFonts,
  listBrandKits,
  STARTER_KIT,
  toBrandKitInput,
} from "@/lib/api/brand-kits";
import { queryKeys } from "@/lib/query/query-keys";
import { BrandKitEditor } from "@/components/brand/brand-kit-editor";
import { PageHeader } from "@/components/app/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useQuery } from "@tanstack/react-query";
import { Palette, Plus } from "lucide-react";
import { useState } from "react";

const NEW = "new";

function KitChip({ kit, selected, onSelect }: { kit: BrandKit; selected: boolean; onSelect: () => void }) {
  const swatches = [kit.colors.primary, kit.colors.secondary, kit.colors.background, kit.colors.surface, kit.colors.text];
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={selected}
      className={`flex w-56 shrink-0 flex-col gap-2.5 rounded-xl border-2 bg-card p-3 text-left transition-[transform,box-shadow] dark:border dark:shadow-none ${
        selected
          ? "-translate-y-0.5 shadow-hard dark:translate-y-0 dark:border-primary dark:bg-primary/8"
          : "hover:-translate-y-0.5 hover:shadow-hard-sm dark:hover:translate-y-0 dark:hover:border-ring/40"
      }`}
    >
      <span className="flex h-7 overflow-hidden rounded-md border-2 dark:border" aria-hidden>
        {swatches.map((color, i) => (
          <span key={i} className="flex-1" style={{ background: color }} />
        ))}
      </span>
      <span className="flex items-center gap-2">
        <span className="min-w-0 flex-1 truncate text-sm font-bold dark:font-semibold">{kit.name}</span>
        {kit.isDefault && <Badge variant="outline">Default</Badge>}
      </span>
    </button>
  );
}

export default function BrandPage() {
  const { data: kits, isLoading, isError } = useQuery({ queryKey: queryKeys.brandKits, queryFn: listBrandKits });
  const { data: fonts } = useQuery({ queryKey: queryKeys.brandFonts, queryFn: listBrandFonts, staleTime: Infinity });
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const creating = selectedId === NEW || (kits !== undefined && kits.length === 0);
  const selected = creating ? undefined : (kits?.find((k) => k.id === selectedId) ?? kits?.[0]);
  const atLimit = (kits?.length ?? 0) >= 10;

  const defaultKit = kits?.find((k) => k.isDefault);
  const eyebrow = kits
    ? kits.length === 0
      ? "No kits yet"
      : `${kits.length} ${kits.length === 1 ? "kit" : "kits"}${defaultKit ? ` · default: ${defaultKit.name}` : ""}`
    : undefined;

  return (
    <div className="mx-auto max-w-6xl space-y-7">
      <PageHeader
        icon={Palette}
        eyebrow={eyebrow}
        title="Brand"
        description="Colors, type, logo and voice that brand blocks and AI drafts start from."
        action={
          kits && kits.length > 0 ? (
            <Button onClick={() => setSelectedId(NEW)} disabled={atLimit || creating} title={atLimit ? "Up to 10 kits" : undefined}>
              <Plus />
              New kit
            </Button>
          ) : undefined
        }
      />

      {isLoading && <p className="text-sm text-muted-foreground">Loading brand kits…</p>}
      {isError && <p className="text-sm text-destructive">Could not load your brand kits.</p>}

      {kits && kits.length > 0 && (
        <div className="-mx-4 flex gap-3 overflow-x-auto px-4 pt-1 pb-2 lg:mx-0 lg:flex-wrap lg:overflow-visible lg:px-0">
          {kits.map((kit) => (
            <KitChip key={kit.id} kit={kit} selected={kit.id === selected?.id} onSelect={() => setSelectedId(kit.id)} />
          ))}
          {creating && (
            <div className="flex w-56 shrink-0 items-center justify-center rounded-xl border-2 border-dashed p-3 text-sm font-semibold text-muted-foreground dark:border">
              New kit (unsaved)
            </div>
          )}
        </div>
      )}

      {kits && kits.length === 0 && (
        <p className="rounded-xl border-2 border-dashed px-4 py-3 text-sm text-muted-foreground dark:border">
          Start your first kit below. It becomes your default: brand blocks in the editor and AI drafts use it.
        </p>
      )}

      {kits && (
        <BrandKitEditor
          key={selected?.id ?? NEW}
          kit={selected}
          initial={selected ? toBrandKitInput(selected) : STARTER_KIT}
          fonts={fonts}
          onSaved={(kit) => setSelectedId(kit.id)}
          onDeleted={() => setSelectedId(null)}
        />
      )}
    </div>
  );
}
