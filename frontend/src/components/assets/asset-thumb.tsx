"use client";

import { getDownloadUrl } from "@/lib/api/assets";
import type { Asset } from "@/lib/schemas/assets";
import { useQuery } from "@tanstack/react-query";
import { FileText } from "lucide-react";

/** Renders an image/GIF thumbnail (via a short-lived URL) or a file-type icon. */
export function AssetThumb({ asset, className }: { asset: Asset; className?: string }) {
  const isImage = asset.kind === "image" || asset.kind === "gif";

  const { data: url } = useQuery({
    queryKey: ["asset-url", asset.id],
    queryFn: () => getDownloadUrl(asset.id),
    enabled: isImage,
    staleTime: 4 * 60 * 1000, // under the 5-min presign expiry
  });

  if (!isImage) {
    return (
      <div className={`flex items-center justify-center bg-muted ${className}`}>
        <FileText className="size-8 text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className={`overflow-hidden bg-muted ${className}`}>
      {url && (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={url} alt={asset.originalFilename} className="h-full w-full object-contain" />
      )}
    </div>
  );
}
