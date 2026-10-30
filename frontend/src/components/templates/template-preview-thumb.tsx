"use client";

import { previewTemplate } from "@/lib/api/templates";
import { cn } from "@/lib/utils";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";

// Emails render at ~600px; the thumbnail renders the real HTML at that width in a
// sandboxed (no-script) iframe, then scales it down to the card width so what you
// see is exactly what recipients get.
const EMAIL_WIDTH = 600;

/** `className` sets the frame's shape (defaults to a 4:3 card). The preview loads once the thumb nears the viewport. */
export function TemplatePreviewThumb({ versionId, className }: { versionId: string | null; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(0.5);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    // Setting state from the observer callbacks (not synchronously in the effect
    // body) is allowed by the set-state-in-effect lint rule.
    const resize = new ResizeObserver(() => setScale(el.clientWidth / EMAIL_WIDTH));
    const intersect = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting) setVisible(true);
      },
      { rootMargin: "200px" },
    );
    resize.observe(el);
    intersect.observe(el);
    return () => {
      resize.disconnect();
      intersect.disconnect();
    };
  }, []);

  const preview = useQuery({
    queryKey: ["preview", "thumb", versionId],
    queryFn: () => previewTemplate({ templateVersionId: versionId!, variables: {}, mode: "sample" }),
    enabled: !!versionId && visible,
    staleTime: Infinity, // versions are immutable — cache forever
    retry: false,
  });

  return (
    <div ref={ref} className={cn("relative aspect-[4/3] w-full overflow-hidden bg-white", className)}>
      {preview.data ? (
        <iframe
          title="Template preview"
          sandbox="allow-same-origin"
          srcDoc={preview.data.html}
          tabIndex={-1}
          aria-hidden
          scrolling="no"
          className="pointer-events-none absolute top-0 left-0 origin-top-left border-0 bg-white"
          style={{ width: EMAIL_WIDTH, height: 1000, transform: `scale(${scale})` }}
        />
      ) : (
        <div className="flex h-full items-center justify-center text-xs text-muted-foreground">
          {!versionId ? "No content yet" : preview.isError ? "Preview unavailable" : "Loading preview…"}
        </div>
      )}
    </div>
  );
}
