"use client";

import { previewTemplate, saveVersion } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/hooks/use-session";
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
import { LogoMark } from "@/components/app/logo-mark";
import { InboxPreviewBar } from "@/components/editor/inbox-preview-bar";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  ChevronDown,
  Download,
  FlaskConical,
  History,
  MoreHorizontal,
  Repeat,
  Send,
} from "lucide-react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
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
  const [testOpen, setTestOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [kindOpen, setKindOpen] = useState(false);
  const { data: user } = useSession();

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

  // ⌘S / Ctrl+S saves a new version, like any desktop editor.
  const saveRef = useRef(() => {});
  useEffect(() => {
    saveRef.current = () => {
      if (dirty && !save.isPending) save.mutate();
    };
  });
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "s") {
        e.preventDefault();
        saveRef.current();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const isMac = typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.platform);
  const sourceLabel = editorKind === "html" ? "HTML" : "MJML";
  const composeHref = `/compose?templateId=${template.id}`;

  return (
    <div className="flex h-dvh min-h-0 flex-col">
      <header className="flex h-14 shrink-0 items-center gap-3 border-b-2 bg-card px-3 dark:border-b">
        <div className="flex min-w-0 flex-1 items-center gap-2.5">
          <Button asChild variant="ghost" size="icon" aria-label="Back to templates">
            <Link href="/templates">
              <ArrowLeft />
            </Link>
          </Button>
          <LogoMark withWordmark={false} className="hidden sm:flex" />
          <div className="min-w-0">
            <p className="flex items-center gap-2 truncate text-sm font-bold dark:font-semibold">
              <span className="truncate">{template.name}</span>
              <span className="shrink-0 rounded-md bg-muted px-1.5 py-px font-mono text-[10px] font-bold text-muted-foreground">
                {sourceLabel}
              </span>
            </p>
            <p className="flex items-center gap-1.5 font-mono text-[11px] text-muted-foreground" aria-live="polite">
              <span
                aria-hidden
                className={`size-1.5 rounded-full ${dirty || save.isPending ? "bg-[#ff7a45]" : "bg-[#22c55e]"}`}
              />
              {save.isPending
                ? "Saving…"
                : dirty
                  ? "Unsaved changes"
                  : version
                    ? `v${version.versionNumber} · saved`
                    : "Not saved yet"}
            </p>
          </div>
        </div>

        <Tabs
          value={tab}
          onValueChange={(value) => {
            if (value === "visual" && tab === "mjml") setGrapesKey((k) => k + 1);
            setTab(value as typeof tab);
          }}
        >
          <TabsList aria-label="Editor mode">
            {showVisual && <TabsTrigger value="visual">Visual</TabsTrigger>}
            <TabsTrigger value="mjml">{sourceLabel}</TabsTrigger>
            <TabsTrigger value="preview">Preview</TabsTrigger>
          </TabsList>
        </Tabs>

        <div className="flex flex-1 items-center justify-end gap-2">
          <ThemeToggle />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="icon" aria-label="More actions">
                <MoreHorizontal />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-52">
              <DropdownMenuItem onSelect={() => exportHtml.mutate()} disabled={exportHtml.isPending}>
                <Download />
                Export HTML
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => setKindOpen(true)} disabled={convert.isPending}>
                <Repeat />
                Switch to {editorKind === "html" ? "MJML" : "HTML"}…
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => setHistoryOpen(true)}>
                <History />
                Version history
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
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
          {/* Send hands off to the full Compose flow with this template preselected; it sends the
              saved version. Kept enabled even when dirty: the visual editor marks the template
              dirty on load, which must not hide the path. The test send needs a saved version. */}
          <div className="flex">
            <Button asChild variant="outline" className="rounded-r-none">
              <Link href={composeHref}>
                <Send />
                Send
              </Link>
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="icon" className="-ml-0.5 rounded-l-none" aria-label="Send options">
                  <ChevronDown />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-60">
                <DropdownMenuItem onSelect={() => setTestOpen(true)} disabled={dirty || !version}>
                  <FlaskConical />
                  {dirty ? "Send a test (save first)" : "Send a test to yourself"}
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <Link href={composeHref}>
                    <Send />
                    Send to recipients…
                  </Link>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
          <Button variant="gradient" onClick={() => save.mutate()} disabled={save.isPending || !dirty}>
            {save.isPending ? "Saving…" : "Save version"}
            <kbd className="hidden rounded border border-current/30 px-1 font-mono text-[10px] opacity-70 lg:inline">
              {isMac ? "⌘S" : "Ctrl S"}
            </kbd>
          </Button>
        </div>
      </header>

      <InboxPreviewBar
        senderName={user?.displayName ?? "You"}
        subject={subject}
        preheader={preheader}
        onSubjectChange={(value) => {
          setSubject(value);
          markDirty();
        }}
        onPreheaderChange={(value) => {
          setPreheader(value);
          markDirty();
        }}
      />

      <div className="flex min-h-0 flex-1">
        <div className="flex min-w-0 flex-1 flex-col">
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

        <aside className="w-80 shrink-0 overflow-auto border-l-2 bg-card dark:border-l">
          <p className="border-b-2 px-3 py-2.5 font-mono text-[11px] font-bold tracking-[0.06em] text-muted-foreground uppercase dark:border-b dark:font-sans dark:font-medium">
            Variables
          </p>
          <VariablePanel
            detected={detected}
            variables={variables}
            onChange={(next) => { setVariables(next); markDirty(); }}
          />
          {showVisual && (
            <>
              <p className="border-y-2 px-3 py-2.5 font-mono text-[11px] font-bold tracking-[0.06em] text-muted-foreground uppercase dark:border-y dark:font-sans dark:font-medium">
                Images
              </p>
              <ImagePlaceholdersPanel currentMjml={source} onChange={applyImagePlaceholder} />
            </>
          )}
        </aside>
      </div>

      <EditorKindSwitcher
        isHtml={editorKind === "html"}
        converting={convert.isPending}
        onConvert={() => convert.mutate()}
        open={kindOpen}
        onOpenChange={setKindOpen}
      />
      <TestSendDialog versionId={version?.id} open={testOpen} onOpenChange={setTestOpen} />
      <VersionHistorySheet
        templateId={template.id}
        onRestored={() => queryClient.invalidateQueries({ queryKey: queryKeys.template(template.id) })}
        open={historyOpen}
        onOpenChange={setHistoryOpen}
      />
    </div>
  );
}
