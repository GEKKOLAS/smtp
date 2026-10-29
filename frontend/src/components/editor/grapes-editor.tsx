"use client";

import { listAssets, setAssetVisibility, uploadFile } from "@/lib/api/assets";
import { useEffect, useRef } from "react";
import "grapesjs/dist/css/grapes.min.css";
import "./grapes-editor-theme.css";

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
        // useCustomTheme=false: the plugin otherwise injects a runtime <style>
        // with hard-coded colors (.gjs-one-bg etc.) that overrides our
        // token-driven theme in grapes-editor-theme.css.
        pluginsOpts: { [mjmlPlugin as unknown as string]: { useCustomTheme: false } },
        // Emails are ~600px wide: frame the desktop view instead of stretching it edge to edge,
        // so it sits on the dotted work surface like the Preview tab. widthMedia stays empty so
        // desktop edits don't land inside a max-width media query. Names match the commands
        // grapesjs-mjml registers (set-device-desktop / -tablet / -mobile).
        deviceManager: {
          devices: [
            { id: "desktop", name: "Desktop", width: "640px", widthMedia: "" },
            { id: "tablet", name: "Tablet", width: "770px", widthMedia: "992px" },
            { id: "mobilePortrait", name: "Mobile portrait", width: "320px", widthMedia: "480px" },
          ],
        },
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
