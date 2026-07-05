"use client";

import { previewTemplate, saveVersion } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import type { Template, TemplateContentInput, TemplateVariable } from "@/lib/schemas/templates";
import { MjmlSourceEditor } from "@/components/editor/mjml-source-editor";
import { PreviewPane } from "@/components/editor/preview-pane";
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
  const editorKind = version?.editorKind ?? "html";
  const showVisual = editorKind !== "html";

  // Local editable state initialized directly from the loaded version (props).
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

  return (
    <div className="flex h-[calc(100vh-4rem)] flex-col">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">
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
          <VersionHistorySheet
            templateId={template.id}
            onRestored={() => queryClient.invalidateQueries({ queryKey: queryKeys.template(template.id) })}
          />
          <Button onClick={() => save.mutate()} disabled={save.isPending || !dirty}>
            {save.isPending ? "Saving…" : "Save version"}
          </Button>
        </div>
      </div>

      <div className="grid gap-3 border-b px-4 py-3 sm:grid-cols-2">
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

        <aside className="w-72 shrink-0 overflow-auto border-l">
          <p className="border-b px-3 py-2 text-sm font-medium">Variables</p>
          <VariablePanel
            detected={detected}
            variables={variables}
            onChange={(next) => { setVariables(next); markDirty(); }}
          />
        </aside>
      </div>
    </div>
  );
}
