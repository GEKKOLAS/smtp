"use client";

import { listAssets } from "@/lib/api/assets";
import {
  type BrandColorName,
  type BrandFont,
  type BrandKit,
  type BrandKitInput,
  type BrandScale,
  brandTokensUrl,
  createBrandKit,
  deleteBrandKit,
  fontStack,
  setDefaultBrandKit,
  updateBrandKit,
} from "@/lib/api/brand-kits";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import { BrandPreview } from "@/components/brand/brand-preview";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, Star, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

const HEX = /^#[0-9a-f]{6}$/i;

const COLOR_FIELDS: { name: BrandColorName; label: string; hint: string }[] = [
  { name: "primary", label: "Primary", hint: "Buttons, highlights" },
  { name: "secondary", label: "Secondary", hint: "Accents, badges" },
  { name: "background", label: "Background", hint: "Behind the email" },
  { name: "surface", label: "Surface", hint: "Cards, content area" },
  { name: "text", label: "Text", hint: "Body copy" },
  { name: "muted", label: "Muted", hint: "Footers, captions" },
  { name: "link", label: "Link", hint: "Inline links" },
];

const SCALE_STEPS: (keyof BrandScale)[] = ["small", "medium", "large"];

function errorText(error: unknown): string {
  if (!(error instanceof ApiError)) return "Something went wrong. Please try again.";
  const fields = error.fieldErrors ? Object.values(error.fieldErrors).flat() : [];
  return fields.length > 0 ? fields.join(" ") : error.message;
}

function ColorField({
  label,
  hint,
  value,
  onChange,
}: {
  label: string;
  hint: string;
  value: string;
  onChange: (value: string) => void;
}) {
  const id = `color-${label.toLowerCase()}`;
  const valid = HEX.test(value);
  return (
    <div className="flex items-center gap-3">
      <input
        type="color"
        aria-label={`${label} color picker`}
        value={valid ? value : "#000000"}
        onChange={(e) => onChange(e.target.value.toUpperCase())}
        className="size-10 shrink-0 cursor-pointer rounded-lg border-2 bg-card p-0.5 dark:border"
      />
      <div className="min-w-0 flex-1">
        <Label htmlFor={id} className="text-xs">
          {label} <span className="font-normal text-muted-foreground">· {hint}</span>
        </Label>
        <Input
          id={id}
          value={value}
          maxLength={7}
          spellCheck={false}
          aria-invalid={!valid}
          onChange={(e) => onChange(e.target.value.trim())}
          className="mt-1 font-mono uppercase"
        />
      </div>
    </div>
  );
}

function ScaleField({
  label,
  max,
  scale,
  onChange,
}: {
  label: string;
  max: number;
  scale: BrandScale;
  onChange: (scale: BrandScale) => void;
}) {
  return (
    <fieldset className="space-y-1.5">
      <legend className="text-xs font-medium">{label} (px)</legend>
      <div className="grid grid-cols-3 gap-2">
        {SCALE_STEPS.map((step) => (
          <label key={step} className="space-y-1">
            <span className="font-mono text-[11px] text-muted-foreground uppercase">{step}</span>
            <Input
              type="number"
              min={0}
              max={max}
              value={scale[step]}
              onChange={(e) => onChange({ ...scale, [step]: Math.max(0, Math.min(max, Number(e.target.value) || 0)) })}
            />
          </label>
        ))}
      </div>
    </fieldset>
  );
}

/**
 * Edits one kit (or a new draft when `kit` is undefined). Keyed by kit id in the
 * page, so switching kits remounts it with fresh state instead of syncing props.
 */
export function BrandKitEditor({
  kit,
  initial,
  fonts,
  onSaved,
  onDeleted,
}: {
  kit?: BrandKit;
  initial: BrandKitInput;
  fonts: BrandFont[] | undefined;
  onSaved: (kit: BrandKit) => void;
  onDeleted: () => void;
}) {
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState<BrandKitInput>(initial);
  const [logoUrl, setLogoUrl] = useState<string | null>(kit?.logoUrl ?? null);

  const { data: images } = useQuery({
    queryKey: queryKeys.assets({ kind: "image" }),
    queryFn: () => listAssets({ kind: "image", pageSize: 48 }),
  });

  const dirty = !kit || JSON.stringify(draft) !== JSON.stringify(initial);
  const colorsValid = Object.values(draft.colors).every((c) => HEX.test(c));
  const canSave = dirty && colorsValid && draft.name.trim().length > 0;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.brandKits });

  const save = useMutation({
    mutationFn: () => (kit ? updateBrandKit(kit.id, draft) : createBrandKit(draft)),
    onSuccess: (saved) => {
      // Put the saved kit in the cache first so the page can select it without a flicker.
      queryClient.setQueryData<BrandKit[]>(queryKeys.brandKits, (old = []) =>
        kit ? old.map((k) => (k.id === saved.id ? saved : k)) : [...old, saved],
      );
      void invalidate();
      // Picking a private image publishes it; refresh the library's access badges.
      void queryClient.invalidateQueries({ queryKey: ["assets"] });
      toast.success(kit ? "Brand kit saved" : "Brand kit created");
      onSaved(saved);
    },
    onError: (error) => toast.error(errorText(error)),
  });

  const makeDefault = useMutation({
    mutationFn: () => setDefaultBrandKit(kit!.id),
    onSuccess: () => {
      void invalidate();
      toast.success(`${kit!.name} is now your default kit`);
    },
    onError: (error) => toast.error(errorText(error)),
  });

  const remove = useMutation({
    mutationFn: () => deleteBrandKit(kit!.id),
    onSuccess: () => {
      queryClient.setQueryData<BrandKit[]>(queryKeys.brandKits, (old = []) => old.filter((k) => k.id !== kit!.id));
      void invalidate();
      toast.success("Brand kit deleted");
      onDeleted();
    },
    onError: (error) => toast.error(errorText(error)),
  });

  const set = <K extends keyof BrandKitInput>(key: K, value: BrandKitInput[K]) =>
    setDraft((d) => ({ ...d, [key]: value }));

  const imageItems = images?.items ?? [];

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,24rem)]">
      <div className="@container space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Identity</CardTitle>
            <CardDescription>The name you pick this kit by, and the logo brand headers use.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="kit-name">Name</Label>
              <Input
                id="kit-name"
                value={draft.name}
                maxLength={80}
                onChange={(e) => set("name", e.target.value)}
                aria-invalid={draft.name.trim().length === 0}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Logo</Label>
              <p className="text-xs text-muted-foreground">
                Private images are made public when you save: emails load the logo by URL.
              </p>
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => {
                    set("logoAssetId", null);
                    setLogoUrl(null);
                  }}
                  aria-pressed={draft.logoAssetId === null}
                  className={`flex size-14 items-center justify-center rounded-lg border-2 text-[11px] text-muted-foreground dark:border ${
                    draft.logoAssetId === null ? "border-foreground bg-muted dark:border-primary" : "border-dashed"
                  }`}
                >
                  None
                </button>
                {imageItems.map((asset) => {
                  const selected = draft.logoAssetId === asset.id;
                  return (
                    <button
                      key={asset.id}
                      type="button"
                      title={asset.originalFilename}
                      aria-pressed={selected}
                      onClick={() => {
                        set("logoAssetId", asset.id);
                        setLogoUrl(asset.publicUrl);
                      }}
                      className={`size-14 shrink-0 overflow-hidden rounded-lg border-2 bg-muted dark:border ${
                        selected
                          ? "border-foreground shadow-hard-sm dark:border-primary dark:shadow-none dark:ring-2 dark:ring-primary/30"
                          : "border-transparent hover:border-muted-foreground/40"
                      }`}
                    >
                      {asset.publicUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img src={asset.publicUrl} alt="" className="h-full w-full object-contain" />
                      ) : (
                        <span className="flex h-full w-full items-center justify-center px-1 text-[9px] break-all text-muted-foreground">
                          {asset.originalFilename}
                        </span>
                      )}
                    </button>
                  );
                })}
                {images && imageItems.length === 0 && (
                  <p className="self-center text-xs text-muted-foreground">Upload a logo under Assets first.</p>
                )}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Colors</CardTitle>
            <CardDescription>Hex values, used literally in the MJML your templates get.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 @xl:grid-cols-2">
            {COLOR_FIELDS.map((field) => (
              <ColorField
                key={field.name}
                label={field.label}
                hint={field.hint}
                value={draft.colors[field.name]}
                onChange={(value) => set("colors", { ...draft.colors, [field.name]: value })}
              />
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Type</CardTitle>
            <CardDescription>
              Web fonts show in Apple Mail and most webmail; Outlook and some apps fall back to the stack.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 @xl:grid-cols-2">
            {(["headingFont", "bodyFont"] as const).map((key) => (
              <div key={key} className="space-y-1.5">
                <Label htmlFor={key}>{key === "headingFont" ? "Headings" : "Body"}</Label>
                <NativeSelect id={key} value={draft[key]} onChange={(e) => set(key, e.target.value)} className="h-9">
                  {fonts?.map((font) => (
                    <option key={font.key} value={font.key}>
                      {font.label}
                      {font.googleFamily ? " (web font)" : ""}
                    </option>
                  ))}
                </NativeSelect>
                <p className="truncate text-sm" style={{ fontFamily: fontStack(fonts, draft[key]) }}>
                  The quick brown fox jumps over 123
                </p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Shape</CardTitle>
            <CardDescription>Button style and the radius and spacing tokens.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="flex flex-wrap items-end gap-6">
              <fieldset className="space-y-1.5">
                <legend className="text-xs font-medium">Button</legend>
                <div className="flex gap-1.5" role="radiogroup" aria-label="Button style">
                  {(["filled", "outline"] as const).map((style) => (
                    <Button
                      key={style}
                      type="button"
                      size="sm"
                      variant={draft.button.style === style ? "default" : "outline"}
                      role="radio"
                      aria-checked={draft.button.style === style}
                      onClick={() => set("button", { ...draft.button, style })}
                    >
                      {style === "filled" ? "Filled" : "Outline"}
                    </Button>
                  ))}
                </div>
              </fieldset>
              <label className="min-w-48 flex-1 space-y-1.5">
                <span className="text-xs font-medium">Button radius · {draft.button.radius}px</span>
                <input
                  type="range"
                  min={0}
                  max={32}
                  value={draft.button.radius}
                  onChange={(e) => set("button", { ...draft.button, radius: Number(e.target.value) })}
                  className="block w-full accent-primary dark:accent-primary"
                />
              </label>
            </div>
            <div className="grid gap-5 @xl:grid-cols-2">
              <ScaleField label="Radius" max={40} scale={draft.radius} onChange={(radius) => set("radius", radius)} />
              <ScaleField
                label="Spacing"
                max={64}
                scale={draft.spacing}
                onChange={(spacing) => set("spacing", spacing)}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Voice</CardTitle>
            <CardDescription>How your emails sound. AI drafts and rewrites follow it.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-1.5">
            <Textarea
              aria-label="Tone of voice"
              value={draft.voice ?? ""}
              maxLength={500}
              rows={3}
              placeholder="e.g. Warm and direct. Short sentences, no emojis, never pushy."
              onChange={(e) => set("voice", e.target.value || null)}
            />
            <p className="text-right font-mono text-[11px] text-muted-foreground">{(draft.voice ?? "").length}/500</p>
          </CardContent>
        </Card>
      </div>

      {/* Below xl the preview and actions come first, so the result is in view on open. */}
      <aside className="order-first space-y-4 xl:sticky xl:top-6 xl:order-none xl:self-start">
        <BrandPreview kit={draft} logoUrl={logoUrl} fonts={fonts} />

        <div className="flex flex-wrap gap-2">
          <Button type="button" disabled={!canSave || save.isPending} onClick={() => save.mutate()} className="flex-1">
            {save.isPending ? "Saving…" : kit ? "Save changes" : "Create kit"}
          </Button>
          {kit && !kit.isDefault && (
            <Button type="button" variant="outline" disabled={makeDefault.isPending} onClick={() => makeDefault.mutate()}>
              <Star />
              Make default
            </Button>
          )}
        </div>
        {!colorsValid && <p className="text-sm text-destructive">Colors must be hex values like #6C5CE7.</p>}

        {kit && (
          <div className="flex flex-wrap items-center gap-2 border-t-2 pt-4 dark:border-t">
            <span className="mr-auto font-mono text-xs font-bold uppercase dark:font-medium dark:text-muted-foreground">
              Design tokens
            </span>
            <Button asChild variant="outline" size="sm">
              <a href={brandTokensUrl(kit.id, "json")} download>
                <Download />
                JSON
              </a>
            </Button>
            <Button asChild variant="outline" size="sm">
              <a href={brandTokensUrl(kit.id, "css")} download>
                <Download />
                CSS
              </a>
            </Button>
            <Dialog>
              <DialogTrigger asChild>
                <Button variant="ghost" size="sm" className="text-destructive">
                  <Trash2 />
                  Delete
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Delete {kit.name}?</DialogTitle>
                  <DialogDescription>
                    Templates keep the colors they already use. This can&apos;t be undone.
                  </DialogDescription>
                </DialogHeader>
                <DialogFooter>
                  <DialogClose asChild>
                    <Button variant="outline">Cancel</Button>
                  </DialogClose>
                  <Button variant="destructive" disabled={remove.isPending} onClick={() => remove.mutate()}>
                    Delete kit
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </div>
        )}
        {kit && dirty && <p className="text-xs text-muted-foreground">Export uses the last saved version.</p>}
      </aside>
    </div>
  );
}
