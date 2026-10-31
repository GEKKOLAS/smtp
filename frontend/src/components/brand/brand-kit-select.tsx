"use client";

import { listBrandKits } from "@/lib/api/brand-kits";
import { queryKeys } from "@/lib/query/query-keys";
import { Label } from "@/components/ui/label";
import { NativeSelect } from "@/components/ui/native-select";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";

/**
 * The brand kit a form uses: the user's pick, else the default kit (or none when
 * `preferNone`). Derived instead of seeded by an effect, so it settles once the
 * kits load without a second render pass.
 */
export function useBrandKitChoice({ preferNone = false }: { preferNone?: boolean } = {}) {
  const { data: kits } = useQuery({ queryKey: queryKeys.brandKits, queryFn: listBrandKits });
  const [choice, setChoice] = useState<string | undefined>(undefined);
  const fallback = preferNone ? "" : (kits?.find((k) => k.isDefault)?.id ?? "");
  const kitId = choice ?? fallback;
  return { kits, kitId, setKitId: setChoice, kit: kits?.find((k) => k.id === kitId) };
}

export function BrandKitSelect({
  id,
  kits,
  value,
  onChange,
  noneLabel = "No brand kit",
}: {
  id: string;
  kits: { id: string; name: string; isDefault: boolean; colors: { primary: string } }[] | undefined;
  value: string;
  onChange: (kitId: string) => void;
  noneLabel?: string;
}) {
  const selected = kits?.find((k) => k.id === value);

  if (kits && kits.length === 0) {
    return (
      <p className="text-xs text-muted-foreground">
        <Link href="/brand" className="text-link underline-offset-4 hover:underline">
          Create a brand kit
        </Link>{" "}
        to give drafts your exact colors, fonts and voice.
      </p>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <Label htmlFor={id} className="shrink-0 text-xs">
        Brand kit
      </Label>
      {selected && (
        <span
          className="size-4 shrink-0 rounded-full border-2 dark:border"
          style={{ background: selected.colors.primary }}
          aria-hidden
        />
      )}
      <NativeSelect id={id} value={value} onChange={(e) => onChange(e.target.value)} className="h-8 max-w-56">
        <option value="">{noneLabel}</option>
        {kits?.map((kit) => (
          <option key={kit.id} value={kit.id}>
            {kit.name}
            {kit.isDefault ? " (default)" : ""}
          </option>
        ))}
      </NativeSelect>
    </div>
  );
}
