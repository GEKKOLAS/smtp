import { z } from "zod";

export const editorKindSchema = z.enum(["visual", "mjml", "html"]);
export type EditorKind = z.infer<typeof editorKindSchema>;

export const variableTypeSchema = z.enum(["text", "url", "html"]);
export type VariableType = z.infer<typeof variableTypeSchema>;

export const templateVariableSchema = z.object({
  name: z.string(),
  type: variableTypeSchema,
  required: z.boolean(),
  default: z.string().nullable(),
  sample: z.string().nullable(),
});
export type TemplateVariable = z.infer<typeof templateVariableSchema>;

export const templateAssetRefSchema = z.object({
  assetId: z.string(),
  usage: z.enum(["inline_cid", "hosted_image", "attachment"]),
  contentId: z.string().nullable(),
});
export type TemplateAssetRef = z.infer<typeof templateAssetRefSchema>;

export const templateVersionSchema = z.object({
  id: z.string(),
  versionNumber: z.number(),
  subject: z.string(),
  preheader: z.string().nullable(),
  editorKind: editorKindSchema,
  mjmlSource: z.string().nullable(),
  grapesProject: z.unknown().nullable(),
  htmlBody: z.string(),
  textBody: z.string().nullable(),
  variablesSchema: z.array(templateVariableSchema),
  assets: z.array(templateAssetRefSchema),
  createdAt: z.string(),
});
export type TemplateVersion = z.infer<typeof templateVersionSchema>;

export const templateSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().nullable(),
  isArchived: z.boolean(),
  createdAt: z.string(),
  updatedAt: z.string(),
  currentVersion: templateVersionSchema.nullable(),
});
export type Template = z.infer<typeof templateSchema>;

export const templateSummarySchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().nullable(),
  isArchived: z.boolean(),
  currentVersionNumber: z.number().nullable(),
  updatedAt: z.string(),
});
export type TemplateSummary = z.infer<typeof templateSummarySchema>;

export const pagedTemplatesSchema = z.object({
  items: z.array(templateSummarySchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export const versionSummarySchema = z.object({
  id: z.string(),
  versionNumber: z.number(),
  editorKind: z.string(),
  createdAt: z.string(),
});
export const pagedVersionsSchema = z.object({
  items: z.array(versionSummarySchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export const previewResultSchema = z.object({
  subject: z.string(),
  preheader: z.string().nullable(),
  html: z.string(),
  text: z.string(),
  warnings: z.array(z.object({ code: z.string(), message: z.string(), line: z.number().nullable() })),
});
export type PreviewResult = z.infer<typeof previewResultSchema>;

// Content payload sent to create/save-version.
export interface TemplateContentInput {
  editorKind: EditorKind;
  subject: string;
  preheader: string | null;
  mjmlSource: string | null;
  grapesProject: unknown | null;
  htmlBody: string;
  textBody: string | null;
  variables: TemplateVariable[];
  assets: TemplateAssetRef[];
}
