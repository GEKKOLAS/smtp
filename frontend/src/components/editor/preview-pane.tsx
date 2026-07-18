"use client";

import type { PreviewResult } from "@/lib/schemas/templates";
import { Button } from "@/components/ui/button";
import { useCallback, useEffect, useRef, useState } from "react";

const MIN_WIDTH = 280;
const MAX_WIDTH = 1000;

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
  const [width, setWidth] = useState(640);
  const dragging = useRef(false);

  const onPointerMove = useCallback((e: PointerEvent) => {
    if (!dragging.current) return;
    const frame = document.getElementById("preview-frame-anchor");
    if (!frame) return;
    // The frame is horizontally centered (mx-auto), so its center stays fixed as
    // width changes — measuring from there (and doubling) tracks the dragged
    // right-edge handle correctly, unlike measuring from the left edge (which
    // itself shifts as the box grows/shrinks).
    const rect = frame.getBoundingClientRect();
    const center = rect.left + rect.width / 2;
    const next = Math.round((e.clientX - center) * 2);
    setWidth(Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, next)));
  }, []);

  const stopDragging = useCallback(() => {
    dragging.current = false;
  }, []);

  useEffect(() => {
    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", stopDragging);
    return () => {
      window.removeEventListener("pointermove", onPointerMove);
      window.removeEventListener("pointerup", stopDragging);
    };
  }, [onPointerMove, stopDragging]);

  return (
    <div className="flex h-full flex-col">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/70 px-3 py-2">
        <div className="flex items-center gap-2">
          <Button size="sm" variant={width === 640 ? "default" : "outline"} onClick={() => setWidth(640)}>
            Desktop
          </Button>
          <Button size="sm" variant={width === 375 ? "default" : "outline"} onClick={() => setWidth(375)}>
            Mobile
          </Button>
          <div className="ml-1 flex items-center gap-1 text-xs text-muted-foreground">
            <input
              type="number"
              min={MIN_WIDTH}
              max={MAX_WIDTH}
              value={width}
              onChange={(e) => {
                const next = Number(e.target.value);
                if (!Number.isNaN(next)) setWidth(Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, next)));
              }}
              className="h-7 w-16 rounded-md border border-input bg-background px-2 text-center text-xs"
              aria-label="Preview width in pixels"
            />
            <span>px</span>
          </div>
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
          <div id="preview-frame-anchor" className="relative mx-auto" style={{ width }}>
            <iframe
              title="Email preview"
              sandbox="allow-same-origin"
              srcDoc={preview.html}
              className="h-[70vh] w-full rounded-md border bg-white shadow-sm"
            />
            <button
              type="button"
              aria-label="Drag to resize the preview"
              onPointerDown={(e) => {
                e.preventDefault();
                dragging.current = true;
              }}
              className="absolute top-1/2 right-0 flex h-10 w-3 -translate-y-1/2 translate-x-1/2 cursor-ew-resize items-center justify-center rounded-full bg-brand/80 shadow-md ring-2 ring-background hover:bg-brand"
            >
              <span className="h-4 w-0.5 rounded-full bg-white/80" />
            </button>
          </div>
        )}
      </div>

      {preview && preview.warnings.length > 0 && (
        <div className="max-h-32 overflow-auto border-t border-border/70 px-3 py-2">
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
