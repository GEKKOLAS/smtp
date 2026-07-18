"use client";

import { previewTemplate, saveVersion } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import type { EditorKind, Template, TemplateContentInput, TemplateVariable } from "@/lib/schemas/templates";
import { applyPlaceholder, type PlaceholderRole } from "@/lib/mjml-placeholders";
import { AiEditDialog } from "@/components/editor/ai-edit-dialog";
import { EditorKindSwitcher } from "@/components/editor/editor-kind-switcher";
import { ImagePlaceholdersPanel } from "@/components/editor/image-placeholders-panel";
import { MjmlSourceEditor } from "@/components/editor/mjml-source-editor";
import { PreviewPane } from "@/components/editor/preview-pane";
import { TestSendDialog } from "@/components/editor/test-send-dialog";
import { VariablePanel, detectVariables } from "@/components/editor/variable-panel";
import { VersionHistorySheet } from "@/components/editor/version-history-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

const GrapesEditor = dynamic(
  () => import("@/components/editor/grapes-editor").then((m) => m.GrapesEditor),
  { ssr: false, loading: () => <p className="p-4 text-sm text-muted-foreground">Loading editor…</p> },
);

export function TemplateEditor({ template }: { template: Template }) {
  const queryClient = useQueryClient();
  const version = template.currentVersion;

  // Local editable state initialized directly from the loaded version (props).
  const [editorKind, setEditorKind] = useState<EditorKind>(version?.editorKind ?? "html");
  const showVisual = editorKind !== "html";
  const [subject, setSubject] = useState(version?.subject ?? "");
  const [preheader, setPreheader] = useState(version?.preheader ?? "");
  const [source, setSource] = useState(
    version ? (editorKind === "html" ? version.htmlBody : version.mjmlSource ?? "") : "",
  );
  const [variables, setVariables] = useState<TemplateVariable[]>(version?.variablesSchema ?? []);
  const [tab, setTab] = useState<"visual" | "mjml" | "preview">(showVisual ? "visual" : "mjml");
  const [grapesKey, setGrapesKey] = useState(0);
  const [dirty, setDirty] = useState(false);

  const detected = useMemo(() => detectVariables(subject, preheader, source), [subject, preheader, source]);

  const content: TemplateContentInput = useMemo(
    () => ({
      editorKind,
      subject,
      preheader: preheader || null,
      mjmlSource: editorKind === "html" ? null : source,
      grapesProject: null,
      htmlBody: editorKind === "html" ? source : "",
      textBody: null,
      variables: detected.map(
        (name) =>
          variables.find((v) => v.name === name) ?? {
            name, type: "text" as const, required: true, default: null, sample: "",
          },
      ),
      assets: [],
    }),
    [editorKind, subject, preheader, source, detected, variables],
  );

  const [debounced, setDebounced] = useState(content);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(content), 500);
    return () => clearTimeout(t);
  }, [content]);

  const preview = useQuery({
    queryKey: ["preview", template.id, JSON.stringify(debounced)],
    queryFn: () => previewTemplate({ content: debounced, variables: {}, mode: "sample" }),
    enabled: tab === "preview",
    retry: false,
  });
  const previewError = preview.error instanceof ApiError ? preview.error.message ?? "Preview failed." : null;

  const save = useMutation({
    mutationFn: () => saveVersion(template.id, content),
    onSuccess: (saved) => {
      setDirty(false);
      queryClient.invalidateQueries({ queryKey: queryKeys.template(template.id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.templateVersions(template.id) });
      toast.success(`Saved version ${saved.versionNumber}`);
    },
    onError: (e) =>
      toast.error(
        e instanceof ApiError && e.errorCode === "template.mjml_invalid"
          ? "The MJML has errors — check the source view."
          : "Could not save the template.",
      ),
  });

  const markDirty = () => setDirty(true);

  const exportHtml = useMutation({
    mutationFn: () => previewTemplate({ content, variables: {}, mode: "sample" }),
    onSuccess: (result) => {
      const blob = new Blob([result.html], { type: "text/html" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${template.name.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}.html`;
      link.click();
      URL.revokeObjectURL(url);
    },
    onError: () => toast.error("Could not export the HTML."),
  });

  const convert = useMutation({
    mutationFn: async () => {
      if (editorKind === "html") {
        // HTML -> MJML: mj-raw passes the existing markup through untouched,
        // so the rendered output is identical while gaining the MJML tooling.
        return { kind: "mjml" as const, next: `<mjml><mj-body><mj-raw>${source}</mj-raw></mj-body></mjml>` };
      }
      // MJML -> HTML: compile+sanitize+inline via the real render pipeline, but
      // feed each variable back as its own "{{name}}" token so the Handlebars
      // substitution is a no-op and the placeholders survive in the output.
      const passthroughVars = Object.fromEntries(detected.map((name) => [name, `{{${name}}}`]));
      const rendered = await previewTemplate({
        content: { ...content, editorKind: "mjml" },
        variables: passthroughVars,
        mode: "sample",
      });
      return { kind: "html" as const, next: rendered.html };
    },
    onSuccess: ({ kind, next }) => {
      setEditorKind(kind);
      setSource(next);
      setTab(kind === "html" ? "mjml" : "visual");
      setGrapesKey((k) => k + 1);
      markDirty();
      toast.success(`Switched to ${kind === "html" ? "HTML" : "MJML"}.`);
    },
    onError: () => toast.error("Could not convert the template."),
  });

  const applyImagePlaceholder = (role: PlaceholderRole, imageUrl: string | null) => {
    setSource((current) => applyPlaceholder(current, role, imageUrl));
    setGrapesKey((k) => k + 1);
    markDirty();
  };

  return (
    <div className="flex h-[calc(100vh-7.5rem)] flex-col overflow-hidden rounded-2xl ring-1 ring-border/70">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border/70 bg-muted/30 px-4 py-3 backdrop-blur-xl">
        <div className="flex items-center gap-3">
          <Button asChild variant="ghost" size="sm">
            <Link href="/templates">← Templates</Link>
          </Button>
          <div>
            <p className="font-medium">{template.name}</p>
            <p className="text-xs text-muted-foreground">
              {dirty ? "Unsaved changes" : `Saved · v${version?.versionNumber}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={() => exportHtml.mutate()} disabled={exportHtml.isPending}>
            Export HTML
          </Button>
          <EditorKindSwitcher
            isHtml={editorKind === "html"}
            converting={convert.isPending}
            onConvert={() => convert.mutate()}
          />
          <AiEditDialog
            currentSource={source}
            isHtml={editorKind === "html"}
            onApplied={(result) => {
              setSubject(result.subject);
              setPreheader(result.preheader ?? "");
              setSource(result.mjmlSource);
              setVariables(
                result.variables.map((v) => ({
                  name: v.name,
                  type: v.type,
                  required: false,
                  default: null,
                  sample: v.sample,
                })),
              );
              setGrapesKey((k) => k + 1);
              markDirty();
              toast.success("AI edit applied — review and save when ready.");
            }}
          />
          <TestSendDialog versionId={version?.id} disabled={dirty} />
          <VersionHistorySheet
            templateId={template.id}
            onRestored={() => queryClient.invalidateQueries({ queryKey: queryKeys.template(template.id) })}
          />
          <Button onClick={() => save.mutate()} disabled={save.isPending || !dirty}>
            {save.isPending ? "Saving…" : "Save version"}
          </Button>
        </div>
      </div>

      <div className="grid gap-3 border-b border-border/70 bg-card/40 px-4 py-3 backdrop-blur-xl sm:grid-cols-2">
        <div>
          <label className="text-xs font-medium text-muted-foreground">Subject</label>
          <Input value={subject} onChange={(e) => { setSubject(e.target.value); markDirty(); }} />
        </div>
        <div>
          <label className="text-xs font-medium text-muted-foreground">Preheader</label>
          <Input value={preheader} onChange={(e) => { setPreheader(e.target.value); markDirty(); }} />
        </div>
      </div>

      <div className="flex min-h-0 flex-1">
        <div className="flex min-w-0 flex-1 flex-col">
          <Tabs
            value={tab}
            onValueChange={(value) => {
              if (value === "visual" && tab === "mjml") setGrapesKey((k) => k + 1);
              setTab(value as typeof tab);
            }}
          >
            <TabsList className="m-2">
              {showVisual && <TabsTrigger value="visual">Visual</TabsTrigger>}
              <TabsTrigger value="mjml">{editorKind === "html" ? "HTML" : "MJML"}</TabsTrigger>
              <TabsTrigger value="preview">Preview</TabsTrigger>
            </TabsList>
          </Tabs>

          <div className="min-h-0 flex-1">
            {tab === "visual" && showVisual && (
              <GrapesEditor
                key={grapesKey}
                initialMjml={source}
                onChange={(mjml) => { setSource(mjml); markDirty(); }}
              />
            )}
            {tab === "mjml" && (
              <MjmlSourceEditor value={source} onChange={(v) => { setSource(v); markDirty(); }} />
            )}
            {tab === "preview" && (
              <PreviewPane preview={preview.data} isLoading={preview.isFetching} error={previewError} />
            )}
          </div>
        </div>

        <aside className="w-72 shrink-0 overflow-auto border-l border-border/70 bg-card/40 backdrop-blur-xl">
          <p className="border-b border-border/70 px-3 py-2 text-sm font-medium">Variables</p>
          <VariablePanel
            detected={detected}
            variables={variables}
            onChange={(next) => { setVariables(next); markDirty(); }}
          />
          {showVisual && (
            <>
              <p className="border-y border-border/70 px-3 py-2 text-sm font-medium">Images</p>
              <ImagePlaceholdersPanel currentMjml={source} onChange={applyImagePlaceholder} />
            </>
          )}
        </aside>
      </div>
    </div>
  );
}
