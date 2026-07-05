"use client";

import type { PreviewResult } from "@/lib/schemas/templates";
import { Button } from "@/components/ui/button";
import { useState } from "react";

/**
 * Renders preview HTML inside a sandboxed iframe (no scripts) so template markup
 * can never execute in the app (spec 04 §3).
 */
export function PreviewPane({
  preview,
  isLoading,
  error,
}: {
  preview: PreviewResult | undefined;
  isLoading: boolean;
  error: string | null;
}) {
  const [device, setDevice] = useState<"desktop" | "mobile">("desktop");
  const width = device === "desktop" ? 640 : 375;

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center justify-between border-b px-3 py-2">
        <div className="flex gap-1">
          <Button size="sm" variant={device === "desktop" ? "default" : "outline"} onClick={() => setDevice("desktop")}>
            Desktop
          </Button>
          <Button size="sm" variant={device === "mobile" ? "default" : "outline"} onClick={() => setDevice("mobile")}>
            Mobile
          </Button>
        </div>
        {preview && (
          <span className="truncate text-xs text-muted-foreground">
            Subject: {preview.subject || "(none)"}
          </span>
        )}
      </div>

      <div className="flex-1 overflow-auto bg-muted/40 p-4">
        {error && <p className="text-sm text-destructive">{error}</p>}
        {isLoading && !preview && <p className="text-sm text-muted-foreground">Rendering…</p>}
        {preview && (
          <iframe
            title="Email preview"
            sandbox="allow-same-origin"
            srcDoc={preview.html}
            className="mx-auto h-[70vh] rounded-md border bg-white shadow-sm"
            style={{ width }}
          />
        )}
      </div>

      {preview && preview.warnings.length > 0 && (
        <div className="max-h-32 overflow-auto border-t px-3 py-2">
          <p className="mb-1 text-xs font-medium text-muted-foreground">Warnings</p>
          <ul className="space-y-1">
            {preview.warnings.map((w, i) => (
              <li key={i} className="text-xs text-amber-600 dark:text-amber-500">
                {w.message}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
