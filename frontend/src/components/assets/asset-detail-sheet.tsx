"use client";

import { deleteAsset, getDownloadUrl, setAssetVisibility } from "@/lib/api/assets";
import { formatBytes, formatDate } from "@/lib/format";
import type { Asset } from "@/lib/schemas/assets";
import { AssetThumb } from "@/components/assets/asset-thumb";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";

export function AssetDetailSheet({
  asset,
  open,
  onOpenChange,
  onChanged,
}: {
  asset: Asset | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged: () => void;
}) {
  const isImage = asset?.kind === "image" || asset?.kind === "gif";

  const visibility = useMutation({
    mutationFn: (access: "public" | "private") => setAssetVisibility(asset!.id, access),
    onSuccess: () => {
      onChanged();
      toast.success("Visibility updated");
    },
    onError: () => toast.error("Could not update visibility"),
  });

  const remove = useMutation({
    mutationFn: () => deleteAsset(asset!.id),
    onSuccess: () => {
      onOpenChange(false);
      onChanged();
      toast.success("Asset deleted");
    },
    onError: () => toast.error("Could not delete asset"),
  });

  async function copyUrl() {
    if (!asset) return;
    const url = asset.publicUrl ?? (await getDownloadUrl(asset.id));
    await navigator.clipboard.writeText(url);
    toast.success(asset.publicUrl ? "Public URL copied" : "Temporary URL copied");
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex flex-col gap-4 sm:max-w-md">
        {asset && (
          <>
            <SheetHeader>
              <SheetTitle className="truncate">{asset.originalFilename}</SheetTitle>
              <SheetDescription>
                {asset.kind} · {formatBytes(asset.sizeBytes)} · uploaded {formatDate(asset.createdAt)}
              </SheetDescription>
            </SheetHeader>

            <div className="px-4">
              <AssetThumb asset={asset} className="flex h-48 items-center justify-center rounded-md" />
            </div>

            <dl className="grid grid-cols-2 gap-2 px-4 text-sm">
              <dt className="text-muted-foreground">Type</dt>
              <dd>{asset.mimeType}</dd>
              {asset.width && asset.height && (
                <>
                  <dt className="text-muted-foreground">Dimensions</dt>
                  <dd>
                    {asset.width} × {asset.height}
                  </dd>
                </>
              )}
              <dt className="text-muted-foreground">Visibility</dt>
              <dd className="capitalize">{asset.access}</dd>
            </dl>

            <div className="mt-auto space-y-2 px-4">
              <Button variant="outline" className="w-full" onClick={copyUrl}>
                Copy URL
              </Button>
              {isImage && (
                <Button
                  variant="outline"
                  className="w-full"
                  disabled={visibility.isPending}
                  onClick={() => visibility.mutate(asset.access === "public" ? "private" : "public")}
                >
                  {asset.access === "public" ? "Make private" : "Make public (hosted)"}
                </Button>
              )}
            </div>

            <SheetFooter>
              <Button
                variant="destructive"
                className="w-full"
                disabled={remove.isPending}
                onClick={() => remove.mutate()}
              >
                {remove.isPending ? "Deleting…" : "Delete asset"}
              </Button>
            </SheetFooter>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}
