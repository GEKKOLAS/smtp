import { api } from "@/lib/api/client";
import {
  pagedTemplatesSchema,
  pagedVersionsSchema,
  type PreviewResult,
  previewResultSchema,
  type Template,
  templateSchema,
  type TemplateContentInput,
  type TemplateVersion,
  templateVersionSchema,
} from "@/lib/schemas/templates";
import { z } from "zod";

export interface ListTemplatesParams {
  search?: string;
  archived?: boolean;
  page?: number;
  pageSize?: number;
}

export function listTemplates(params: ListTemplatesParams = {}) {
  const query = new URLSearchParams();
  if (params.search) query.set("search", params.search);
  query.set("archived", String(params.archived ?? false));
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 24));
  return api(`/templates?${query.toString()}`, { schema: pagedTemplatesSchema });
}

export function getTemplate(id: string): Promise<Template> {
  return api(`/templates/${id}`, { schema: templateSchema });
}

export function createTemplate(input: {
  name: string;
  description?: string | null;
  content: TemplateContentInput;
}): Promise<Template> {
  return api("/templates", { body: input, schema: templateSchema });
}

export function updateTemplate(id: string, input: { name?: string; description?: string | null }): Promise<Template> {
  return api(`/templates/${id}`, { method: "PATCH", body: input, schema: templateSchema });
}

export function duplicateTemplate(id: string): Promise<Template> {
  return api(`/templates/${id}/duplicate`, { method: "POST", schema: templateSchema });
}

export function archiveTemplate(id: string, archived: boolean): Promise<void> {
  return api(`/templates/${id}/${archived ? "archive" : "unarchive"}`, { method: "POST" });
}

export function deleteTemplate(id: string): Promise<void> {
  return api(`/templates/${id}`, { method: "DELETE" });
}

export function saveVersion(templateId: string, content: TemplateContentInput): Promise<TemplateVersion> {
  return api(`/templates/${templateId}/versions`, { body: content, schema: templateVersionSchema });
}

export function listVersions(templateId: string) {
  return api(`/templates/${templateId}/versions?pageSize=50`, { schema: pagedVersionsSchema });
}

export function getVersion(templateId: string, versionId: string): Promise<TemplateVersion> {
  return api(`/templates/${templateId}/versions/${versionId}`, { schema: templateVersionSchema });
}

export function restoreVersion(templateId: string, versionId: string): Promise<TemplateVersion> {
  return api(`/templates/${templateId}/versions/${versionId}/restore`, {
    method: "POST",
    schema: templateVersionSchema,
  });
}

export interface PreviewInput {
  templateVersionId?: string | null;
  content?: TemplateContentInput | null;
  variables: Record<string, string | null>;
  mode: "sample" | "strict";
}

export function previewTemplate(input: PreviewInput): Promise<PreviewResult> {
  return api("/render/preview", { body: input, schema: previewResultSchema });
}

const validateResultSchema = z.object({
  valid: z.boolean(),
  errors: z.array(z.unknown()),
  warnings: z.array(z.object({ code: z.string(), message: z.string(), line: z.number().nullable() })),
});

export function validateTemplate(input: PreviewInput) {
  return api("/render/validate", { body: input, schema: validateResultSchema });
}
