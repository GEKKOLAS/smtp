"use client";

import { listAssets } from "@/lib/api/assets";
import { formatBytes } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import type { Asset } from "@/lib/schemas/assets";
import { PageHeader } from "@/components/app/page-header";
import { AssetDetailSheet } from "@/components/assets/asset-detail-sheet";
import { AssetThumb } from "@/components/assets/asset-thumb";
import { UploadDropzone } from "@/components/assets/upload-dropzone";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Image as ImageIcon } from "lucide-react";
import { useMemo, useState } from "react";

const TABS = [
  { value: "all", label: "All", kind: undefined },
  { value: "image", label: "Images", kind: "image" },
  { value: "gif", label: "GIFs", kind: "gif" },
  { value: "document", label: "Documents", kind: "document" },
];

export default function AssetsPage() {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState("all");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Asset | null>(null);

  const kind = useMemo(() => TABS.find((t) => t.value === tab)?.kind, [tab]);

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.assets({ kind, search }),
    queryFn: () => listAssets({ kind, search: search || undefined, pageSize: 48 }),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["assets"] });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <PageHeader
        icon={ImageIcon}
        title="Asset library"
        description="Upload images, GIFs, and files to use in your templates."
      />

      <UploadDropzone onUploaded={refresh} />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            {TABS.map((t) => (
              <TabsTrigger key={t.value} value={t.value}>
                {t.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
        <Input
          placeholder="Search by filename…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full sm:w-64"
        />
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading assets…</p>}
      {isError && <p className="text-sm text-destructive">Could not load your assets.</p>}

      {data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed py-16 text-center">
          <p className="text-sm text-muted-foreground">
            {search ? "No assets match your search." : "No assets yet — upload your first file above."}
          </p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
          {data.items.map((asset) => (
            <li key={asset.id}>
              <button
                type="button"
                onClick={() => setSelected(asset)}
                className="group w-full overflow-hidden rounded-lg border text-left transition-colors hover:border-foreground/30"
              >
                <div className="relative aspect-square">
                  <AssetThumb asset={asset} className="flex h-full w-full items-center justify-center" />
                  {asset.access === "public" && (
                    <Badge className="absolute right-1.5 top-1.5" variant="secondary">
                      Public
                    </Badge>
                  )}
                </div>
                <div className="border-t p-2">
                  <p className="truncate text-xs font-medium">{asset.originalFilename}</p>
                  <p className="text-xs text-muted-foreground">{formatBytes(asset.sizeBytes)}</p>
                </div>
              </button>
            </li>
          ))}
        </ul>
      )}

      <AssetDetailSheet
        asset={selected}
        open={selected !== null}
        onOpenChange={(open) => !open && setSelected(null)}
        onChanged={refresh}
      />
    </div>
  );
}
