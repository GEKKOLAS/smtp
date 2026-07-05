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
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

export function VersionHistorySheet({
  templateId,
  onRestored,
}: {
  templateId: string;
  onRestored: () => void;
}) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);

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

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="outline" size="sm">History</Button>
      </SheetTrigger>
      <SheetContent className="sm:max-w-sm">
        <SheetHeader>
          <SheetTitle>Version history</SheetTitle>
          <SheetDescription>Restore a previous version as a new version.</SheetDescription>
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
              <Button
                variant="outline"
                size="sm"
                disabled={restore.isPending}
                onClick={() => restore.mutate(version.id)}
              >
                Restore
              </Button>
            </li>
          ))}
          {data && data.items.length === 0 && (
            <li className="py-3 text-sm text-muted-foreground">No versions yet.</li>
          )}
        </ul>
      </SheetContent>
    </Sheet>
  );
}
