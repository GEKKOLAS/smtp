"use client";

import { createTemplate } from "@/lib/api/templates";
import { ApiError } from "@/lib/api/client";
import { starterContent } from "@/lib/templates-defaults";
import type { EditorKind } from "@/lib/schemas/templates";
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

const START_OPTIONS: { kind: EditorKind; label: string; hint: string }[] = [
  { kind: "visual", label: "Visual builder", hint: "Drag-and-drop blocks (MJML)" },
  { kind: "mjml", label: "MJML source", hint: "Write MJML directly" },
  { kind: "html", label: "Blank HTML", hint: "Start from raw HTML" },
];

export function NewTemplateDialog() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [kind, setKind] = useState<EditorKind>("visual");
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => createTemplate({ name: name.trim(), content: starterContent(kind) }),
    onSuccess: (template) => {
      queryClient.invalidateQueries({ queryKey: ["templates"] });
      setOpen(false);
      router.push(`/templates/${template.id}/edit`);
    },
    onError: (e) =>
      setError(e instanceof ApiError && e.errorCode === "template.name_taken"
        ? "A template with this name already exists."
        : "Could not create the template."),
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
            <div className="grid gap-2">
              {START_OPTIONS.map((opt) => (
                <button
                  key={opt.kind}
                  type="button"
                  onClick={() => setKind(opt.kind)}
                  className={`rounded-md border px-3 py-2 text-left text-sm transition-colors ${
                    kind === opt.kind ? "border-primary bg-primary/5" : "hover:bg-muted"
                  }`}
                >
                  <span className="font-medium">{opt.label}</span>
                  <span className="block text-xs text-muted-foreground">{opt.hint}</span>
                </button>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button
            onClick={() => {
              setError(null);
              if (name.trim().length === 0) {
                setError("Name is required.");
                return;
              }
              create.mutate();
            }}
            disabled={create.isPending}
          >
            {create.isPending ? "Creating…" : "Create & edit"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
