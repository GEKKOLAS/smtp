"use client";

import { previewTemplate } from "@/lib/api/templates";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";

// Emails render at ~600px; the thumbnail renders the real HTML at that width in a
// sandboxed (no-script) iframe, then scales it down to the card width so what you
// see is exactly what recipients get.
const EMAIL_WIDTH = 600;

export function TemplatePreviewThumb({ versionId }: { versionId: string | null }) {
  const ref = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(0.5);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    // Setting state from the observer callback (not synchronously in the effect
    // body) is allowed by the set-state-in-effect lint rule.
    const observer = new ResizeObserver(() => setScale(el.clientWidth / EMAIL_WIDTH));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const preview = useQuery({
    queryKey: ["preview", "thumb", versionId],
    queryFn: () => previewTemplate({ templateVersionId: versionId!, variables: {}, mode: "sample" }),
    enabled: !!versionId,
    staleTime: Infinity, // versions are immutable — cache forever
    retry: false,
  });

  return (
    <div ref={ref} className="relative aspect-[4/3] w-full overflow-hidden bg-white">
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
