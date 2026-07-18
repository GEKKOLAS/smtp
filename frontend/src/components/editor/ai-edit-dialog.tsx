"use client";

import { generateTemplate } from "@/lib/api/ai";
import { listAssets } from "@/lib/api/assets";
import { ApiError } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Sparkles, Wand2 } from "lucide-react";
import { useState } from "react";

export type AiEditResult = {
  subject: string;
  preheader: string | null;
  mjmlSource: string;
  variables: { name: string; type: "text" | "url" | "html"; sample: string }[];
};

function AssetRoleField({
  label,
  hint,
  assets,
  selectedId,
  onSelect,
}: {
  label: string;
  hint: string;
  assets: { id: string; originalFilename: string; publicUrl: string | null }[];
  selectedId: string | null;
  onSelect: (id: string | null) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs">{label}</Label>
      <p className="text-xs text-muted-foreground">{hint}</p>
      <div className="flex flex-wrap gap-2">
        {assets.map((asset) => {
          const selected = selectedId === asset.id;
          return (
            <button
              key={asset.id}
              type="button"
              title={asset.originalFilename}
              onClick={() => onSelect(selected ? null : asset.id)}
              className={`size-10 shrink-0 overflow-hidden rounded-md border-2 bg-muted ${
                selected ? "border-brand ring-2 ring-brand/30" : "border-transparent hover:border-muted-foreground/30"
              }`}
            >
              {asset.publicUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={asset.publicUrl} alt="" className="h-full w-full object-cover" />
              ) : (
                <span className="flex h-full w-full items-center justify-center text-[9px] text-muted-foreground">
                  private
                </span>
              )}
            </button>
          );
        })}
        {assets.length === 0 && <p className="text-xs text-muted-foreground">No images in your asset library yet.</p>}
      </div>
    </div>
  );
}

export function AiEditDialog({
  currentSource,
  isHtml,
  onApplied,
}: {
  currentSource: string;
  isHtml: boolean;
  onApplied: (result: AiEditResult) => void;
}) {
  const [open, setOpen] = useState(false);
  const [prompt, setPrompt] = useState("");
  const [advancedModel, setAdvancedModel] = useState(false);
  const [backgroundAssetId, setBackgroundAssetId] = useState<string | null>(null);
  const [headerLogoAssetId, setHeaderLogoAssetId] = useState<string | null>(null);
  const [footerLogoAssetId, setFooterLogoAssetId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const assetPicker = useQuery({
    queryKey: ["assets", "picker", "image"],
    queryFn: () => listAssets({ kind: "image", pageSize: 12 }),
    enabled: open,
  });
  const assets = assetPicker.data?.items ?? [];

  const edit = useMutation({
    mutationFn: () =>
      generateTemplate({
        prompt,
        useAdvancedModel: advancedModel,
        backgroundImageAssetId: backgroundAssetId ?? undefined,
        headerLogoAssetId: headerLogoAssetId ?? undefined,
        footerLogoAssetId: footerLogoAssetId ?? undefined,
        ...(isHtml ? { currentHtml: currentSource } : { currentMjml: currentSource }),
      }),
    onSuccess: (g) => {
      onApplied({
        subject: g.subject,
        preheader: g.preheader,
        mjmlSource: g.mjmlSource,
        variables: g.variables.map((v) => ({
          name: v.name,
          type: (["text", "url", "html"].includes(v.type) ? v.type : "text") as "text" | "url" | "html",
          sample: v.sample,
        })),
      });
      setOpen(false);
      setPrompt("");
    },
    onError: (e) =>
      setError(
        e instanceof ApiError && e.errorCode === "ai.generation_failed"
          ? "The AI couldn't apply that change — try rephrasing."
          : "Could not edit the template.",
      ),
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setError(null);
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Wand2 className="size-4" />
          Edit with AI
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Edit with AI</DialogTitle>
          <DialogDescription>
            Describe the change — the AI revises this template in place, keeping the rest intact.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {error && <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>}

          <div className="space-y-2">
            <Label htmlFor="ai-edit-prompt">What should change?</Label>
            <textarea
              id="ai-edit-prompt"
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="Make the CTA button orange and add a 20% off coupon section"
              rows={3}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm"
              autoFocus
            />
          </div>

          <div className="space-y-3 rounded-lg border border-dashed p-3">
            <p className="text-xs font-medium">Placeholder images (optional)</p>
            <AssetRoleField
              label="Background image"
              hint="Used behind the hero/section background."
              assets={assets}
              selectedId={backgroundAssetId}
              onSelect={setBackgroundAssetId}
            />
            <AssetRoleField
              label="Header logo"
              hint="Placed small at the very top of the email."
              assets={assets}
              selectedId={headerLogoAssetId}
              onSelect={setHeaderLogoAssetId}
            />
            <AssetRoleField
              label="Footer logo"
              hint="Placed small in the footer/signature area."
              assets={assets}
              selectedId={footerLogoAssetId}
              onSelect={setFooterLogoAssetId}
            />
          </div>

          <div className="space-y-1.5">
            <Label className="text-xs">Design model</Label>
            <div className="grid gap-2 sm:grid-cols-2">
              <button
                type="button"
                onClick={() => setAdvancedModel(false)}
                className={`flex items-start gap-2 rounded-xl border px-3 py-2 text-left text-sm transition-all ${
                  !advancedModel
                    ? "border-brand/40 bg-linear-to-br from-brand/10 to-brand-2/10 ring-1 ring-brand/25"
                    : "border-border hover:bg-muted"
                }`}
              >
                <Sparkles className={`mt-0.5 size-4 shrink-0 ${!advancedModel ? "text-brand" : "text-muted-foreground"}`} />
                <span>
                  <span className="block font-medium">Standard</span>
                  <span className="block text-xs text-muted-foreground">Fast, great for most edits</span>
                </span>
              </button>
              <button
                type="button"
                onClick={() => setAdvancedModel(true)}
                className={`flex items-start gap-2 rounded-xl border px-3 py-2 text-left text-sm transition-all ${
                  advancedModel
                    ? "border-brand/40 bg-linear-to-br from-brand/10 to-brand-2/10 ring-1 ring-brand/25"
                    : "border-border hover:bg-muted"
                }`}
              >
                <Wand2 className={`mt-0.5 size-4 shrink-0 ${advancedModel ? "text-brand" : "text-muted-foreground"}`} />
                <span>
                  <span className="block font-medium">Advanced design</span>
                  <span className="block text-xs text-muted-foreground">Slower, best for bigger reworks</span>
                </span>
              </button>
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button
            onClick={() => {
              setError(null);
              if (prompt.trim().length === 0) return setError("Describe the change to make.");
              edit.mutate();
            }}
            disabled={edit.isPending}
          >
            {edit.isPending ? "Applying…" : "Apply changes"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
