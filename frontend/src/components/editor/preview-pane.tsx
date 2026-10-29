"use client";

import type { PreviewResult } from "@/lib/schemas/templates";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Minus, Moon, Plus } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

const MIN_WIDTH = 280;
const MAX_WIDTH = 1000;
const ZOOM_STEPS = [0.5, 0.75, 1, 1.25];

// Approximates a client's forced dark mode: invert the page, then invert images back so
// photos and logos keep their real colors. Real clients differ — this is a quick sanity check.
const DARK_INBOX_CSS =
  "<style>html{filter:invert(1) hue-rotate(180deg);background:#fff}img,picture,video,[style*='background-image']{filter:invert(1) hue-rotate(180deg)}</style>";

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
  const [zoom, setZoom] = useState(1);
  const [darkInbox, setDarkInbox] = useState(false);
  const dragging = useRef(false);
  const zoomRef = useRef(zoom);

  useEffect(() => {
    zoomRef.current = zoom;
  }, [zoom]);

  const onPointerMove = useCallback((e: PointerEvent) => {
    if (!dragging.current) return;
    const frame = document.getElementById("preview-frame-anchor");
    if (!frame) return;
    // The frame is horizontally centered (mx-auto), so its center stays fixed as
    // width changes — measuring from there (and doubling) tracks the dragged
    // right-edge handle correctly, unlike measuring from the left edge (which
    // itself shifts as the box grows/shrinks). Screen pixels are divided by the
    // zoom to get back to email pixels.
    const rect = frame.getBoundingClientRect();
    const center = rect.left + rect.width / 2;
    const next = Math.round(((e.clientX - center) * 2) / zoomRef.current);
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

  const zoomIndex = ZOOM_STEPS.indexOf(zoom);
  const device = width === 640 ? "Desktop" : width === 375 ? "Mobile" : "Custom";

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex shrink-0 flex-wrap items-center justify-between gap-2 border-b-2 bg-card px-3 py-2 dark:border-b">
        <div className="flex items-center gap-2">
          <div role="group" aria-label="Preview device" className="flex gap-1">
            <Button size="sm" variant={width === 640 ? "default" : "outline"} onClick={() => setWidth(640)}>
              Desktop
            </Button>
            <Button size="sm" variant={width === 375 ? "default" : "outline"} onClick={() => setWidth(375)}>
              Mobile
            </Button>
          </div>
          <label className="flex items-center gap-1 text-xs text-muted-foreground">
            <Input
              type="number"
              min={MIN_WIDTH}
              max={MAX_WIDTH}
              value={width}
              onChange={(e) => {
                const next = Number(e.target.value);
                if (!Number.isNaN(next)) setWidth(Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, next)));
              }}
              className="h-7 w-16 px-1.5 text-center font-mono text-xs md:text-xs"
              aria-label="Preview width in pixels"
            />
            px
          </label>
        </div>
        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant={darkInbox ? "default" : "outline"}
            aria-pressed={darkInbox}
            onClick={() => setDarkInbox((on) => !on)}
            title="Approximates how dark-mode mail clients recolor this email"
          >
            <Moon />
            Dark inbox
          </Button>
          <div className="flex items-center gap-1" role="group" aria-label="Zoom">
            <Button
              size="icon-sm"
              variant="outline"
              aria-label="Zoom out"
              disabled={zoomIndex <= 0}
              onClick={() => setZoom(ZOOM_STEPS[zoomIndex - 1])}
            >
              <Minus />
            </Button>
            <span className="w-11 text-center font-mono text-xs">{Math.round(zoom * 100)}%</span>
            <Button
              size="icon-sm"
              variant="outline"
              aria-label="Zoom in"
              disabled={zoomIndex >= ZOOM_STEPS.length - 1}
              onClick={() => setZoom(ZOOM_STEPS[zoomIndex + 1])}
            >
              <Plus />
            </Button>
          </div>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-auto bg-background bg-[radial-gradient(color-mix(in_oklab,var(--foreground)_16%,transparent)_1px,transparent_1px)] bg-size-[16px_16px] px-6 py-6 dark:bg-[radial-gradient(color-mix(in_oklab,var(--foreground)_8%,transparent)_1px,transparent_1px)]">
        {error && <p className="text-sm text-destructive">{error}</p>}
        {isLoading && !preview && <p className="text-sm text-muted-foreground">Rendering…</p>}
        {preview && (
          <div style={{ zoom }}>
            <p className="mx-auto mb-2.5 font-mono text-[11px] text-muted-foreground" style={{ width }}>
              {device} · {width}px{darkInbox ? " · dark inbox (approx.)" : ""}
            </p>
            <div id="preview-frame-anchor" className="relative mx-auto" style={{ width }}>
              <iframe
                title="Email preview"
                sandbox="allow-same-origin"
                srcDoc={darkInbox ? DARK_INBOX_CSS + preview.html : preview.html}
                className="w-full rounded-xl border-2 border-foreground bg-white shadow-hard-lg dark:border dark:border-white/10 dark:shadow-[0_30px_60px_-24px_rgba(0,0,0,0.9)]"
                style={{ height: `calc((100dvh - 15rem) / ${zoom})`, minHeight: 480 }}
              />
              <button
                type="button"
                aria-label="Drag to resize the preview"
                onPointerDown={(e) => {
                  e.preventDefault();
                  dragging.current = true;
                }}
                className="absolute top-1/2 right-0 flex h-10 w-3 translate-x-1/2 -translate-y-1/2 cursor-ew-resize items-center justify-center rounded-full border-2 border-foreground bg-primary hover:brightness-110 dark:border-background"
              >
                <span className="h-4 w-0.5 rounded-full bg-primary-foreground/70" />
              </button>
            </div>
          </div>
        )}
      </div>

      {preview && preview.warnings.length > 0 && (
        <div className="max-h-32 shrink-0 overflow-auto border-t-2 bg-card px-3 py-2 dark:border-t">
          <p className="mb-1 font-mono text-[11px] font-bold tracking-wide text-warning uppercase">
            {preview.warnings.length} {preview.warnings.length === 1 ? "warning" : "warnings"}
          </p>
          <ul className="space-y-1">
            {preview.warnings.map((w, i) => (
              <li key={i} className="text-xs text-warning">
                {w.line != null && <span className="mr-1.5 font-mono">L{w.line}</span>}
                {w.message}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
