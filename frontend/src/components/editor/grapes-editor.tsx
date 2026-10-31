"use client";

import { listAssets, setAssetVisibility, uploadFile } from "@/lib/api/assets";
import { listBrandFonts, listBrandKits } from "@/lib/api/brand-kits";
import { brandBlocks } from "@/lib/brand-blocks";
import type { RewriteTarget } from "@/components/editor/copy-rewrite-dialog";
import { useEffect, useRef } from "react";
import "grapesjs/dist/css/grapes.min.css";
import "./grapes-editor-theme.css";

interface GrapesEditorProps {
  initialMjml: string;
  onChange: (mjml: string, projectData: unknown) => void;
  /** Present when the AI copy assistant is available: adds "Rewrite" to text and button toolbars. */
  onRewriteText?: (target: RewriteTarget) => void;
}

const REWRITABLE: Record<string, RewriteTarget["kind"]> = { "mj-text": "body", "mj-button": "cta" };

const SPARKLE_ICON =
  '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z"/></svg>';

function escapeText(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

/**
 * Visual email builder (GrapesJS + grapesjs-mjml). The asset manager is wired to
 * the user's public images; uploads go through our presigned flow and are
 * auto-published so the resulting URL is stable in saved templates.
 */
export function GrapesEditor({ initialMjml, onChange, onRewriteText }: GrapesEditorProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const onChangeRef = useRef(onChange);
  const onRewriteRef = useRef(onRewriteText);

  useEffect(() => {
    onChangeRef.current = onChange;
    onRewriteRef.current = onRewriteText;
  }, [onChange, onRewriteText]);

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

      // Blocks drawn from the default brand kit, with its values written in.
      try {
        const [kits, fonts] = await Promise.all([listBrandKits(), listBrandFonts()]);
        const kit = kits.find((k) => k.isDefault) ?? kits[0];
        if (kit && !disposed) {
          const label = `Brand · ${kit.name}`.replace(/[<>&"]/g, "");
          const category = { id: "brand", label, open: true };
          for (const block of brandBlocks(kit, fonts)) {
            editor.BlockManager.add(block.id, {
              label: block.label,
              category,
              content: block.content,
              media: block.media,
            });
          }
        }
      } catch {
        // Non-fatal: the editor works without brand blocks.
      }

      // "Rewrite with AI" on the toolbar of a selected text block or button.
      editor.Commands.add("mth-rewrite", {
        run(ed) {
          const component = ed.getSelected();
          const kind = component && REWRITABLE[component.get("type") ?? ""];
          const open = onRewriteRef.current;
          if (!component || !kind || !open) return;

          const html = component.getInnerHTML();
          const text = (component.getEl()?.innerText ?? html.replace(/<[^>]+>/g, "")).trim();
          if (!text) return;
          open({
            kind,
            text,
            hasMarkup: /<(?!br\b)[a-z]/i.test(html),
            apply: (next) => component.components(escapeText(next).replace(/\n/g, "<br>")),
          });
        },
      });
      editor.on("component:selected", (component: import("grapesjs").Component) => {
        if (!onRewriteRef.current || !REWRITABLE[component.get("type") ?? ""]) return;
        const toolbar = component.get("toolbar") ?? [];
        if (toolbar.some((item: { command?: unknown }) => item.command === "mth-rewrite")) return;
        component.set("toolbar", [
          { label: SPARKLE_ICON, command: "mth-rewrite", attributes: { title: "Rewrite with AI" } },
          ...toolbar,
        ]);
      });

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
