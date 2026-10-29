"use client";

import { generateImage, getAiCapabilities, type ImageSize } from "@/lib/api/ai";
import { ApiError } from "@/lib/api/client";
import type { Asset } from "@/lib/schemas/assets";
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
import { Textarea } from "@/components/ui/textarea";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { type ReactNode, useState } from "react";
import { toast } from "sonner";

const SIZES: { value: ImageSize; label: string }[] = [
  { value: "1024x1024", label: "Square" },
  { value: "1536x1024", label: "Landscape" },
  { value: "1024x1536", label: "Portrait" },
];

/**
 * Generate an image from a prompt (OpenAI). The result is saved as a public
 * asset and handed back via onGenerated. Renders nothing when the feature isn't
 * configured (no API key) so the button never appears broken.
 */
export function GenerateImageDialog({
  trigger,
  onGenerated,
}: {
  trigger?: ReactNode;
  onGenerated?: (asset: Asset) => void;
}) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [prompt, setPrompt] = useState("");
  const [size, setSize] = useState<ImageSize>("1024x1024");
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<Asset | null>(null);

  const capabilities = useQuery({ queryKey: ["ai", "capabilities"], queryFn: getAiCapabilities });

  const generate = useMutation({
    mutationFn: () => generateImage({ prompt: prompt.trim(), size }),
    onSuccess: (asset) => {
      setResult(asset);
      queryClient.invalidateQueries({ queryKey: ["assets"] });
      onGenerated?.(asset);
      toast.success("Image generated and added to your assets");
    },
    onError: (e) =>
      setError(
        e instanceof ApiError
          ? e.message ?? "Could not generate the image."
          : "Could not generate the image.",
      ),
  });

  if (capabilities.data && !capabilities.data.imageGeneration) return null;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setError(null);
          setResult(null);
        }
      }}
    >
      <DialogTrigger asChild>
        {trigger ?? (
          <Button variant="gradient" size="sm">
            <Sparkles className="size-4" />
            Generate with AI
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Generate an image</DialogTitle>
          <DialogDescription>
            Describe the image — it&apos;s created and saved to your assets, ready to use in emails.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {error && <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>}

          {result?.publicUrl ? (
            <div className="space-y-3">
              <div className="overflow-hidden rounded-lg border bg-muted">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={result.publicUrl} alt="" className="mx-auto max-h-64 w-auto object-contain" />
              </div>
              <p className="text-center text-xs text-muted-foreground">
                Saved as <span className="font-medium">{result.originalFilename}</span> in your assets.
              </p>
            </div>
          ) : (
            <>
              <div className="space-y-2">
                <Label htmlFor="img-prompt">Describe the image</Label>
                <Textarea
                  id="img-prompt"
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  placeholder="A minimalist geometric logo of a coffee cup, deep navy and gold, flat vector"
                  rows={3}
                  autoFocus
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">Shape</Label>
                <div className="grid grid-cols-3 gap-2">
                  {SIZES.map((s) => (
                    <button
                      key={s.value}
                      type="button"
                      onClick={() => setSize(s.value)}
                      className={`rounded-xl border px-3 py-2 text-sm transition-all ${
                        size === s.value
                          ? "border-brand/40 bg-linear-to-br from-brand/10 to-brand-2/10 ring-1 ring-brand/25"
                          : "border-border hover:bg-muted"
                      }`}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>
              </div>
              <p className="text-xs text-muted-foreground">Generating takes ~10–30 seconds.</p>
            </>
          )}
        </div>

        <DialogFooter>
          {result ? (
            <Button
              onClick={() => {
                setResult(null);
                setPrompt("");
              }}
              variant="outline"
            >
              Generate another
            </Button>
          ) : (
            <Button
              variant="gradient"
              onClick={() => {
                setError(null);
                if (prompt.trim().length === 0) return setError("Describe the image to generate.");
                generate.mutate();
              }}
              disabled={generate.isPending}
            >
              {generate.isPending ? "Generating…" : "Generate"}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
