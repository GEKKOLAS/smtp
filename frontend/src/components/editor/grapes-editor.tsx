"use client";

import { listAssets, setAssetVisibility, uploadFile } from "@/lib/api/assets";
import { useEffect, useRef } from "react";
import "grapesjs/dist/css/grapes.min.css";

interface GrapesEditorProps {
  initialMjml: string;
  onChange: (mjml: string, projectData: unknown) => void;
}

/**
 * Visual email builder (GrapesJS + grapesjs-mjml). The asset manager is wired to
 * the user's public images; uploads go through our presigned flow and are
 * auto-published so the resulting URL is stable in saved templates.
 */
export function GrapesEditor({ initialMjml, onChange }: GrapesEditorProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const onChangeRef = useRef(onChange);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    let editor: import("grapesjs").Editor | undefined;
    let disposed = false;

    (async () => {
      const [{ default: grapesjs }, { default: mjmlPlugin }] = await Promise.all([
        import("grapesjs"),
        import("grapesjs-mjml"),
      ]);
      if (disposed || !containerRef.current) return;

      editor = grapesjs.init({
        container: containerRef.current,
        height: "100%",
        storageManager: false,
        plugins: [mjmlPlugin],
        pluginsOpts: { [mjmlPlugin as unknown as string]: {} },
        assetManager: {
          uploadFile: async (event: DragEvent | Event) => {
            const input = (event as DragEvent).dataTransfer ?? (event.target as HTMLInputElement);
            const files = (input as DataTransfer | HTMLInputElement).files;
            if (!files) return;
            for (const file of Array.from(files)) {
              try {
                const asset = await uploadFile(file);
                const published = await setAssetVisibility(asset.id, "public");
                if (published.publicUrl) editor?.AssetManager.add(published.publicUrl);
              } catch {
                // Ignored; the dropzone in the asset library surfaces errors.
              }
            }
          },
        },
      });

      // Preload the user's public images/GIFs into the asset manager.
      try {
        const [images, gifs] = await Promise.all([
          listAssets({ kind: "image", pageSize: 100 }),
          listAssets({ kind: "gif", pageSize: 100 }),
        ]);
        const urls = [...images.items, ...gifs.items]
          .filter((a) => a.access === "public" && a.publicUrl)
          .map((a) => a.publicUrl!);
        if (urls.length) editor.AssetManager.add(urls);
      } catch {
        // Non-fatal.
      }

      editor.setComponents(initialMjml);

      const emit = () => {
        if (!editor) return;
        onChangeRef.current(editor.getHtml(), editor.getProjectData());
      };
      editor.on("update", emit);
      editor.on("component:update", emit);
    })();

    return () => {
      disposed = true;
      editor?.destroy();
    };
    // Initialize once; parent controls remounting via a key when the source changes externally.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return <div ref={containerRef} className="h-full min-h-0" />;
}
