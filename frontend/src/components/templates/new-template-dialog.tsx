"use client";

import { generateTemplate } from "@/lib/api/ai";
import { createTemplate } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { starterContent } from "@/lib/templates-defaults";
import type { EditorKind, TemplateContentInput } from "@/lib/schemas/templates";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";

type StartMode = EditorKind | "import" | "ai";

const START_OPTIONS: { mode: StartMode; label: string; hint: string }[] = [
  { mode: "ai", label: "✨ Generate with AI", hint: "Describe it, get a draft" },
  { mode: "visual", label: "Visual builder", hint: "Drag-and-drop blocks (MJML)" },
  { mode: "mjml", label: "MJML source", hint: "Write MJML directly" },
  { mode: "html", label: "Blank HTML", hint: "Start from raw HTML5" },
  { mode: "import", label: "Import HTML", hint: "Paste an existing HTML email" },
];

export function NewTemplateDialog() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [mode, setMode] = useState<StartMode>("ai");
  const [importHtml, setImportHtml] = useState("");
  const [prompt, setPrompt] = useState("");
  const [brandColor, setBrandColor] = useState("#2563eb");
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: async (): Promise<TemplateContentInput> => {
      if (mode === "ai") {
        const g = await generateTemplate({ prompt, brandColor });
        return {
          editorKind: "mjml",
          subject: g.subject,
          preheader: null,
          mjmlSource: g.mjmlSource,
          grapesProject: null,
          htmlBody: "",
          textBody: null,
          variables: g.variables.map((v) => ({
            name: v.name,
            type: (["text", "url", "html"].includes(v.type) ? v.type : "text") as "text" | "url" | "html",
            required: false,
            default: null,
            sample: v.sample,
          })),
          assets: [],
        };
      }
      if (mode === "import") return { ...starterContent("html"), htmlBody: importHtml };
      return starterContent(mode);
    },
    onSuccess: async (content) => {
      const template = await createTemplate({ name: name.trim(), content });
      queryClient.invalidateQueries({ queryKey: ["templates"] });
      setOpen(false);
      router.push(`/templates/${template.id}/edit`);
    },
    onError: (e) =>
      setError(
        e instanceof ApiError && e.errorCode === "template.name_taken"
          ? "A template with this name already exists."
          : e instanceof ApiError && e.errorCode === "ai.generation_failed"
            ? "The AI couldn't generate a template — try rephrasing your prompt."
            : "Could not create the template.",
      ),
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>New template</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create a template</DialogTitle>
          <DialogDescription>Give it a name and choose how you want to start.</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {error && <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>}
          <div className="space-y-2">
            <Label htmlFor="template-name">Name</Label>
            <Input
              id="template-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Welcome email"
              autoFocus
            />
          </div>
          <div className="space-y-2">
            <Label>Start from</Label>
            <div className="grid gap-2 sm:grid-cols-2">
              {START_OPTIONS.map((opt) => (
                <button
                  key={opt.mode}
                  type="button"
                  onClick={() => setMode(opt.mode)}
                  className={`rounded-md border px-3 py-2 text-left text-sm transition-colors ${
                    mode === opt.mode ? "border-primary bg-primary/5" : "hover:bg-muted"
                  }`}
                >
                  <span className="font-medium">{opt.label}</span>
                  <span className="block text-xs text-muted-foreground">{opt.hint}</span>
                </button>
              ))}
            </div>
          </div>

          {mode === "ai" && (
            <div className="space-y-2">
              <Label htmlFor="ai-prompt">Describe your email</Label>
              <textarea
                id="ai-prompt"
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                placeholder="A warm welcome email for new subscribers to my coffee shop, with a 10% off first order button"
                rows={3}
                className="w-full rounded-md border bg-background px-3 py-2 text-sm"
              />
              <div className="flex items-center gap-2">
                <Label htmlFor="ai-color" className="text-xs">Brand color</Label>
                <input
                  id="ai-color"
                  type="color"
                  value={brandColor}
                  onChange={(e) => setBrandColor(e.target.value)}
                  className="h-7 w-10 rounded border"
                />
              </div>
            </div>
          )}

          {mode === "import" && (
            <div className="space-y-2">
              <Label htmlFor="import-html">HTML</Label>
              <textarea
                id="import-html"
                value={importHtml}
                onChange={(e) => setImportHtml(e.target.value)}
                placeholder="<html>…</html>"
                rows={6}
                className="w-full rounded-md border bg-background px-3 py-2 font-mono text-xs"
              />
              <p className="text-xs text-muted-foreground">
                Content is sanitized and CSS is inlined for email on every render.
              </p>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            onClick={() => {
              setError(null);
              if (name.trim().length === 0) return setError("Name is required.");
              if (mode === "ai" && prompt.trim().length === 0) return setError("Describe the email to generate.");
              if (mode === "import" && importHtml.trim().length === 0) return setError("Paste some HTML to import.");
              create.mutate();
            }}
            disabled={create.isPending}
          >
            {create.isPending ? (mode === "ai" ? "Generating…" : "Creating…") : "Create & edit"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
