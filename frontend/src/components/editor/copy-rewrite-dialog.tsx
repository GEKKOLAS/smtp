"use client";

import { type CopyKind, type CopyTone, type CopyVariation, rewriteCopy } from "@/lib/api/ai";
import { ApiError } from "@/lib/api/client";
import { BrandKitSelect, useBrandKitChoice } from "@/components/brand/brand-kit-select";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useMutation } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { useState } from "react";

const TONES: { value: CopyTone; label: string }[] = [
  { value: "promotional", label: "Promotional" },
  { value: "professional", label: "Professional" },
  { value: "technical", label: "Technical" },
  { value: "followUp", label: "Follow-up" },
];

const KIND_LABEL: Record<CopyKind, string> = {
  subject: "subject line",
  preheader: "preheader",
  body: "text",
  cta: "button label",
};

export type RewriteTarget = {
  kind: CopyKind;
  text: string;
  /** Inline links or bold would be flattened to plain text. */
  hasMarkup?: boolean;
  apply: (text: string) => void;
};

function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.errorCode === "ai.generation_failed") return "The suggestions didn't keep your {{variables}}. Try again.";
    if (error.errorCode === "ai.unavailable") return "The copy assistant isn't set up on this server.";
    if (error.status === 429) return "Too many requests. Wait a moment and try again.";
  }
  return "Could not get suggestions. Try again.";
}

/**
 * Picks a tone, asks for three rewrites, and applies the chosen one. Several
 * targets share one dialog (subject, preheader, a text block), so the parent
 * keys it by target to start fresh each time.
 */
export function CopyRewriteDialog({
  target,
  context,
  onClose,
}: {
  target: RewriteTarget;
  /** Facts the model may rely on, e.g. the template's subject. */
  context?: string;
  onClose: () => void;
}) {
  const [tone, setTone] = useState<CopyTone>("professional");
  const brand = useBrandKitChoice();
  const suggest = useMutation({
    mutationFn: () =>
      rewriteCopy({
        kind: target.kind,
        text: target.text,
        tone,
        count: 3,
        brandKitId: brand.kitId || undefined,
        context: context?.trim() || undefined,
      }),
  });

  const pick = (variation: CopyVariation) => {
    target.apply(variation.text);
    onClose();
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Rewrite the {KIND_LABEL[target.kind]}</DialogTitle>
          <DialogDescription>
            {"Pick a tone. Suggestions keep your {{variables}} and add no facts you didn't write."}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <blockquote className="rounded-lg border-2 border-dashed px-3 py-2 text-sm whitespace-pre-wrap text-muted-foreground dark:border">
            {target.text}
          </blockquote>
          {target.hasMarkup && (
            <p className="text-xs text-warning">
              This block has links or formatting. A suggestion replaces it with plain text.
            </p>
          )}

          <div className="flex flex-wrap gap-1.5" role="radiogroup" aria-label="Tone">
            {TONES.map((t) => (
              <Button
                key={t.value}
                type="button"
                size="sm"
                role="radio"
                aria-checked={tone === t.value}
                variant={tone === t.value ? "default" : "outline"}
                onClick={() => setTone(t.value)}
              >
                {t.label}
              </Button>
            ))}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <BrandKitSelect id="copy-brand-kit" kits={brand.kits} value={brand.kitId} onChange={brand.setKitId} noneLabel="No brand voice" />
            <Button type="button" onClick={() => suggest.mutate()} disabled={suggest.isPending}>
              <Sparkles />
              {suggest.isPending ? "Writing…" : suggest.data ? "Try again" : "Suggest"}
            </Button>
          </div>

          {suggest.isError && (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {errorText(suggest.error)}
            </p>
          )}

          {suggest.data && (
            <ul className="space-y-2" aria-label="Suggestions">
              {suggest.data.map((variation) => (
                <li key={variation.text}>
                  <button
                    type="button"
                    onClick={() => pick(variation)}
                    className="group w-full rounded-xl border-2 bg-card px-3.5 py-2.5 text-left transition-[transform,box-shadow] hover:-translate-y-0.5 hover:shadow-hard-sm dark:border dark:hover:translate-y-0 dark:hover:border-ring/50 dark:hover:shadow-none"
                  >
                    <span className="block text-sm font-semibold whitespace-pre-wrap">{variation.text}</span>
                    {variation.note && (
                      <span className="mt-0.5 block text-xs text-muted-foreground">{variation.note}</span>
                    )}
                    <span className="mt-1 block font-mono text-[11px] font-bold text-link uppercase opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100 dark:font-medium dark:normal-case">
                      Use this
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
