"use client";

import { getTemplate } from "@/lib/api/templates";
import { queryKeys } from "@/lib/query/query-keys";
import type { Template } from "@/lib/schemas/templates";
import { TemplateEditor } from "@/components/editor/template-editor";
import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";

export default function TemplateEditorPage() {
  const templateId = useParams().id as string;

  const { data: template, isLoading, isError } = useQuery({
    queryKey: queryKeys.template(templateId),
    queryFn: () => getTemplate(templateId),
  });

  if (isLoading) return <p className="p-8 text-sm text-muted-foreground">Loading template…</p>;
  if (isError || !template) return <p className="p-8 text-sm text-destructive">Could not load template.</p>;

  // Re-mount the editor when the current version changes (e.g. after restore) so
  // its local state re-initializes from props — no seeding effect needed.
  return <TemplateEditor key={editorSeed(template)} template={template} />;
}

function editorSeed(template: Template): string {
  return `${template.id}:${template.currentVersion?.id ?? "none"}`;
}
