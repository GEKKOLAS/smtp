"use client";

import { listVersions, restoreVersion } from "@/lib/api/templates";
import { formatDate } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { VersionCompareDialog } from "@/components/editor/version-compare-dialog";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { GitCompare } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

export function VersionHistorySheet({
  templateId,
  onRestored,
  open: controlledOpen,
  onOpenChange,
}: {
  templateId: string;
  onRestored: () => void;
  /** Pass with onOpenChange to open it from elsewhere (e.g. a menu); no trigger button is rendered then. */
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const [innerOpen, setInnerOpen] = useState(false);
  const [compare, setCompare] = useState<{ before: string; after: string } | null>(null);
  const controlled = controlledOpen !== undefined;
  const open = controlled ? controlledOpen : innerOpen;
  const setOpen = (next: boolean) => (controlled ? onOpenChange?.(next) : setInnerOpen(next));

  const { data } = useQuery({
    queryKey: queryKeys.templateVersions(templateId),
    queryFn: () => listVersions(templateId),
    enabled: open,
  });

  const restore = useMutation({
    mutationFn: (versionId: string) => restoreVersion(templateId, versionId),
    onSuccess: (version) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.template(templateId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.templateVersions(templateId) });
      setOpen(false);
      onRestored();
      toast.success(`Restored as version ${version.versionNumber}`);
    },
    onError: () => toast.error("Could not restore this version"),
  });

  // Versions are listed newest first.
  const latest = data?.items[0];
  const openCompare = (before: string, after: string) => {
    setOpen(false);
    setCompare({ before, after });
  };

  return (
    <>
    <Sheet open={open} onOpenChange={setOpen}>
      {!controlled && (
        <SheetTrigger asChild>
          <Button variant="outline" size="sm">History</Button>
        </SheetTrigger>
      )}
      <SheetContent className="sm:max-w-sm">
        <SheetHeader>
          <SheetTitle>Version history</SheetTitle>
          <SheetDescription>Compare any version with the latest, or restore it as a new version.</SheetDescription>
          {data && data.items.length > 1 && latest && (
            <Button
              variant="outline"
              size="sm"
              className="mt-2 self-start"
              onClick={() => openCompare(data.items[1].id, latest.id)}
            >
              <GitCompare />
              Compare latest two
            </Button>
          )}
        </SheetHeader>
        <ul className="divide-y px-4">
          {data?.items.map((version) => (
            <li key={version.id} className="flex items-center justify-between gap-3 py-3">
              <div>
                <p className="text-sm font-medium">Version {version.versionNumber}</p>
                <p className="text-xs text-muted-foreground">
                  {version.editorKind} · {formatDate(version.createdAt)}
                </p>
              </div>
              <div className="flex shrink-0 gap-1.5">
                {latest && version.id !== latest.id && (
                  <Button variant="ghost" size="sm" onClick={() => openCompare(version.id, latest.id)}>
                    Compare
                  </Button>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  disabled={restore.isPending}
                  onClick={() => restore.mutate(version.id)}
                >
                  Restore
                </Button>
              </div>
            </li>
          ))}
          {data && data.items.length === 0 && (
            <li className="py-3 text-sm text-muted-foreground">No versions yet.</li>
          )}
        </ul>
      </SheetContent>
    </Sheet>
    {compare && (
      <VersionCompareDialog
        templateId={templateId}
        initialBefore={compare.before}
        initialAfter={compare.after}
        onClose={() => setCompare(null)}
        onRestored={() => {
          setCompare(null);
          onRestored();
        }}
      />
    )}
    </>
  );
}
