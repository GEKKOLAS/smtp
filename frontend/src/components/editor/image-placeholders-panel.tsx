"use client";

import { listAssets } from "@/lib/api/assets";
import { detectPlaceholders, type PlaceholderRole } from "@/lib/mjml-placeholders";
import { Label } from "@/components/ui/label";
import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";

const ROLES: { role: PlaceholderRole; label: string; hint: string }[] = [
  { role: "headerLogo", label: "Header logo", hint: "Small, at the very top." },
  { role: "background", label: "Background image", hint: "Behind its own hero band." },
  { role: "footerLogo", label: "Footer logo", hint: "Small, at the very bottom." },
];

export function ImagePlaceholdersPanel({
  currentMjml,
  onChange,
}: {
  currentMjml: string;
  onChange: (role: PlaceholderRole, imageUrl: string | null) => void;
}) {
  const assetPicker = useQuery({
    queryKey: ["assets", "picker", "public-image"],
    queryFn: () => listAssets({ kind: "image", pageSize: 24 }),
  });
  const publicAssets = (assetPicker.data?.items ?? []).filter((a) => a.access === "public" && a.publicUrl);
  const current = useMemo(() => detectPlaceholders(currentMjml), [currentMjml]);

  return (
    <div className="space-y-4 p-3">
      <p className="text-xs text-muted-foreground">
        Assign images directly — no AI involved. Only public assets can be used here, since their
        URL is permanent.{" "}
        {publicAssets.length === 0 && "Make an asset public from the Assets page to use it."}
      </p>
      {ROLES.map(({ role, label, hint }) => {
        const selectedUrl = current[role];
        return (
          <div key={role} className="space-y-1.5">
            <Label className="text-xs">{label}</Label>
            <p className="text-[11px] text-muted-foreground">{hint}</p>
            <div className="flex flex-wrap gap-2">
              {publicAssets.map((asset) => {
                const selected = selectedUrl === asset.publicUrl;
                return (
                  <button
                    key={asset.id}
                    type="button"
                    title={asset.originalFilename}
                    onClick={() => onChange(role, selected ? null : asset.publicUrl!)}
                    className={`size-10 shrink-0 overflow-hidden rounded-md border-2 bg-muted ${
                      selected ? "border-brand ring-2 ring-brand/30" : "border-transparent hover:border-muted-foreground/30"
                    }`}
                  >
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img src={asset.publicUrl!} alt="" className="h-full w-full object-cover" />
                  </button>
                );
              })}
              {publicAssets.length === 0 && (
                <p className="text-xs text-muted-foreground">No public images yet.</p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
