"use client";

import { getVersion, listVersions, previewTemplate, restoreVersion } from "@/lib/api/templates";
import { formatDate } from "@/lib/format";
import { useIsDesktop } from "@/lib/hooks/use-media-query";
import { queryKeys } from "@/lib/query/query-keys";
import type { TemplateVersion } from "@/lib/schemas/templates";
import { diffFields, diffVariables, versionSource } from "@/lib/version-diff";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { NativeSelect } from "@/components/ui/native-select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { html } from "@codemirror/lang-html";
import { MergeView, unifiedMergeView } from "@codemirror/merge";
import { oneDark } from "@codemirror/theme-one-dark";
import { EditorView, lineNumbers } from "@codemirror/view";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";

const editorTheme = EditorView.theme({
  "&": { fontSize: "12.5px" },
  ".cm-scroller": { fontFamily: "var(--font-mono, monospace)" },
  ".cm-gutters": { backgroundColor: "transparent", border: "none" },
});
const readOnly = [html(), oneDark, editorTheme, lineNumbers(), EditorView.editable.of(false), EditorView.lineWrapping];
const collapse = { margin: 3, minSize: 4 };

/** Side by side on desktop; one unified column on phones. Read-only either way. */
function CodeDiff({ before, after, sideBySide }: { before: string; after: string; sideBySide: boolean }) {
  const host = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!host.current) return;
    if (sideBySide) {
      const view = new MergeView({
        a: { doc: before, extensions: readOnly },
        b: { doc: after, extensions: readOnly },
        parent: host.current,
        collapseUnchanged: collapse,
        gutter: true,
      });
      return () => view.destroy();
    }
    const view = new EditorView({
      doc: after,
      extensions: [...readOnly, unifiedMergeView({ original: before, collapseUnchanged: collapse, mergeControls: false })],
      parent: host.current,
    });
    return () => view.destroy();
  }, [before, after, sideBySide]);

  return <div ref={host} className="min-h-40 overflow-hidden rounded-lg bg-[#0d1117]" />;
}

/** A script-free frame sized to its content, so both versions scroll together with the dialog. */
function Rendered({ title, htmlDoc, width }: { title: string; htmlDoc: string | undefined; width: number }) {
  // Start short: scrollHeight can only grow past the frame's own height, never shrink below it.
  const [height, setHeight] = useState(160);

  // A srcdoc frame can finish loading before React would attach onLoad, so listen
  // here and also measure right away when the document is already complete.
  const measure = useCallback((frame: HTMLIFrameElement | null) => {
    if (!frame) return;
    const fit = () => {
      const doc = frame.contentDocument;
      const content = doc?.body ? Math.max(doc.documentElement.scrollHeight, doc.body.scrollHeight) : 0;
      if (content > 0) setHeight(content);
    };
    frame.addEventListener("load", fit);
    if (frame.contentDocument?.readyState === "complete") fit();
    return () => frame.removeEventListener("load", fit);
  }, []);

  if (htmlDoc === undefined) {
    return <p className="py-10 text-center text-sm text-muted-foreground">Rendering…</p>;
  }
  return (
    <iframe
      ref={measure}
      title={title}
      // No allow-scripts: same-origin only lets the dialog read the height.
      sandbox="allow-same-origin"
      srcDoc={htmlDoc}
      style={{ width, height }}
      // content-box: the measured height is the document's, and the border goes outside it.
      className="mx-auto box-content block max-w-full rounded-lg border-2 bg-white dark:border"
    />
  );
}

function VersionPreview({ templateId, version, width }: { templateId: string; version: TemplateVersion; width: number }) {
  const preview = useQuery({
    queryKey: ["version-preview", templateId, version.id],
    queryFn: () => previewTemplate({ templateVersionId: version.id, content: null, variables: {}, mode: "sample" }),
    staleTime: Infinity,
  });
  if (preview.isError) return <p className="py-10 text-center text-sm text-destructive">This version doesn&apos;t render.</p>;
  return (
    <Rendered
      key={`${version.id}:${width}`}
      title={`Version ${version.versionNumber}`}
      htmlDoc={preview.data?.html}
      width={width}
    />
  );
}

function Fields({ before, after }: { before: TemplateVersion; after: TemplateVersion }) {
  const fields = diffFields(before, after);
  const variables = diffVariables(before.variablesSchema, after.variablesSchema);
  if (fields.length === 0 && variables.length === 0) {
    return <p className="py-6 text-sm text-muted-foreground">Subject, preheader, plain text and variables are the same.</p>;
  }
  return (
    <div className="space-y-5">
      {fields.length > 0 && (
        <dl className="space-y-3">
          {fields.map((change) => (
            <div key={change.field} className="rounded-lg border-2 p-3 dark:border">
              <dt className="mb-1.5 font-mono text-[11px] font-bold tracking-[0.06em] text-muted-foreground uppercase dark:font-sans dark:font-medium dark:normal-case">
                {change.field}
              </dt>
              <dd className="grid gap-1.5 text-sm sm:grid-cols-2">
                <p className="rounded-md bg-destructive/8 px-2 py-1 whitespace-pre-wrap line-through decoration-destructive/50">
                  {change.before}
                </p>
                <p className="rounded-md bg-success-soft px-2 py-1 whitespace-pre-wrap dark:bg-success-soft">{change.after}</p>
              </dd>
            </div>
          ))}
        </dl>
      )}
      {variables.length > 0 && (
        <div>
          <p className="mb-2 text-sm font-semibold">Variables</p>
          <ul className="space-y-1.5 text-sm">
            {variables.map((change) => (
              <li key={`${change.kind}-${change.name}`} className="flex flex-wrap items-baseline gap-2">
                <span className="font-mono text-xs">{`{{${change.name}}}`}</span>
                <span className="text-muted-foreground">
                  {change.kind === "added"
                    ? `added (${change.variable.type})`
                    : change.kind === "removed"
                      ? "removed"
                      : change.details.join(", ")}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/**
 * Compares two versions of a template: rendered side by side, the markup as a
 * diff, and the fields around it. Either side can be restored as a new version.
 */
export function VersionCompareDialog({
  templateId,
  initialBefore,
  initialAfter,
  onClose,
  onRestored,
}: {
  templateId: string;
  initialBefore: string;
  initialAfter: string;
  onClose: () => void;
  onRestored: () => void;
}) {
  const queryClient = useQueryClient();
  const isDesktop = useIsDesktop();
  const [beforeId, setBeforeId] = useState(initialBefore);
  const [afterId, setAfterId] = useState(initialAfter);
  const [width, setWidth] = useState(640);
  const [side, setSide] = useState<"before" | "after">("after");

  const versions = useQuery({ queryKey: queryKeys.templateVersions(templateId), queryFn: () => listVersions(templateId) });
  const before = useQuery({ queryKey: ["template-version", templateId, beforeId], queryFn: () => getVersion(templateId, beforeId) });
  const after = useQuery({ queryKey: ["template-version", templateId, afterId], queryFn: () => getVersion(templateId, afterId) });

  const restore = useMutation({
    mutationFn: (versionId: string) => restoreVersion(templateId, versionId),
    onSuccess: (version) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.template(templateId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.templateVersions(templateId) });
      toast.success(`Restored as version ${version.versionNumber}`);
      onRestored();
    },
    onError: () => toast.error("Could not restore this version"),
  });

  const picker = (id: string, value: string, onChange: (value: string) => void) => (
    <NativeSelect id={id} value={value} onChange={(e) => onChange(e.target.value)} className="h-8 w-auto min-w-36">
      {versions.data?.items.map((v) => (
        <option key={v.id} value={v.id}>
          v{v.versionNumber} · {formatDate(v.createdAt)}
        </option>
      ))}
    </NativeSelect>
  );

  const a = before.data;
  const b = after.data;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="h-[calc(100dvh-2rem)] grid-rows-[auto_minmax(0,1fr)] sm:max-w-[min(96vw,1400px)]">
        <DialogHeader>
          <DialogTitle>Compare versions</DialogTitle>
          <DialogDescription className="sr-only">Pick two versions to see what changed.</DialogDescription>
          <div className="flex flex-wrap items-center gap-2 pt-1">
            {picker("compare-before", beforeId, setBeforeId)}
            <span aria-hidden className="text-muted-foreground">→</span>
            {picker("compare-after", afterId, setAfterId)}
            <div className="ml-auto flex gap-1.5">
              {a && (
                <Button size="sm" variant="outline" disabled={restore.isPending} onClick={() => restore.mutate(a.id)}>
                  Restore v{a.versionNumber}
                </Button>
              )}
              {b && (
                <Button size="sm" variant="outline" disabled={restore.isPending} onClick={() => restore.mutate(b.id)}>
                  Restore v{b.versionNumber}
                </Button>
              )}
            </div>
          </div>
        </DialogHeader>

        {!a || !b ? (
          <p className="text-sm text-muted-foreground">{before.isError || after.isError ? "Could not load a version." : "Loading…"}</p>
        ) : (
          <Tabs defaultValue="visual" className="min-h-0">
            <TabsList>
              <TabsTrigger value="visual">Visual</TabsTrigger>
              <TabsTrigger value="code">Code</TabsTrigger>
              <TabsTrigger value="fields">Fields</TabsTrigger>
            </TabsList>

            <TabsContent value="visual" className="min-h-0 space-y-3 overflow-auto">
              <div className="flex flex-wrap items-center gap-1.5">
                {[640, 375].map((w) => (
                  <Button key={w} size="sm" variant={width === w ? "default" : "outline"} onClick={() => setWidth(w)}>
                    {w === 640 ? "Desktop" : "Mobile"} · {w}px
                  </Button>
                ))}
                {!isDesktop && (
                  <div className="ml-auto flex gap-1.5" role="radiogroup" aria-label="Version shown">
                    {(["before", "after"] as const).map((s) => (
                      <Button
                        key={s}
                        size="sm"
                        role="radio"
                        aria-checked={side === s}
                        variant={side === s ? "default" : "outline"}
                        onClick={() => setSide(s)}
                      >
                        v{s === "before" ? a.versionNumber : b.versionNumber}
                      </Button>
                    ))}
                  </div>
                )}
              </div>
              <div className={isDesktop ? "grid grid-cols-2 gap-4" : ""}>
                {(isDesktop || side === "before") && (
                  <section aria-label={`Version ${a.versionNumber}`} className="min-w-0 space-y-2">
                    <p className="text-center font-mono text-xs text-muted-foreground">v{a.versionNumber}</p>
                    <VersionPreview templateId={templateId} version={a} width={width} />
                  </section>
                )}
                {(isDesktop || side === "after") && (
                  <section aria-label={`Version ${b.versionNumber}`} className="min-w-0 space-y-2">
                    <p className="text-center font-mono text-xs text-muted-foreground">v{b.versionNumber}</p>
                    <VersionPreview templateId={templateId} version={b} width={width} />
                  </section>
                )}
              </div>
            </TabsContent>

            <TabsContent value="code" className="min-h-0 overflow-auto">
              {(a.editorKind === "html") !== (b.editorKind === "html") && (
                <p className="mb-2 text-xs text-muted-foreground">
                  These versions use different formats (MJML and HTML), so most lines differ.
                </p>
              )}
              <CodeDiff before={versionSource(a)} after={versionSource(b)} sideBySide={isDesktop} />
            </TabsContent>

            <TabsContent value="fields" className="min-h-0 overflow-auto">
              <Fields before={a} after={b} />
            </TabsContent>
          </Tabs>
        )}
      </DialogContent>
    </Dialog>
  );
}
