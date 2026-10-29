"use client";

import { listAssets } from "@/lib/api/assets";
import { formatBytes } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import type { Asset } from "@/lib/schemas/assets";
import { EmptyState } from "@/components/app/empty-state";
import { PageHeader } from "@/components/app/page-header";
import { AssetDetailSheet } from "@/components/assets/asset-detail-sheet";
import { AssetThumb } from "@/components/assets/asset-thumb";
import { GenerateImageDialog } from "@/components/assets/generate-image-dialog";
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
        eyebrow={data ? `${data.totalCount} ${data.totalCount === 1 ? "file" : "files"}` : undefined}
        title="Asset library"
        description="Upload images, GIFs, and files to use in your templates."
        action={<GenerateImageDialog onGenerated={refresh} />}
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
        <EmptyState
          icon={ImageIcon}
          title={search ? "No assets match your search" : "No assets yet"}
          description={search ? undefined : "Drop your first image or file in the box above, or generate one with AI."}
        />
      )}

      {data && data.items.length > 0 && (
        <ul className="grid grid-cols-2 gap-5 sm:grid-cols-3 md:grid-cols-4">
          {data.items.map((asset) => (
            <li key={asset.id} className="odd:-rotate-[0.6deg] even:rotate-[0.5deg] dark:odd:rotate-0 dark:even:rotate-0">
              <button
                type="button"
                onClick={() => setSelected(asset)}
                className="group w-full overflow-hidden rounded-xl border-2 bg-card text-left shadow-hard press dark:border dark:shadow-none dark:hover:border-input"
              >
                <div className="relative aspect-square">
                  <AssetThumb asset={asset} className="flex h-full w-full items-center justify-center" />
                  {asset.access === "public" && (
                    <Badge className="absolute top-2 right-2" variant="secondary">
                      Public
                    </Badge>
                  )}
                </div>
                <div className="border-t-2 px-2.5 py-2 dark:border-t">
                  <p className="truncate text-xs font-bold dark:font-medium">{asset.originalFilename}</p>
                  <p className="font-mono text-[11px] text-muted-foreground">{formatBytes(asset.sizeBytes)}</p>
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
