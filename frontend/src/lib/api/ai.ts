import { api } from "@/lib/api/client";
import { z } from "zod";

const generatedTemplateSchema = z.object({
  subject: z.string(),
  mjmlSource: z.string(),
  htmlBody: z.string(),
  variables: z.array(z.object({ name: z.string(), type: z.string(), sample: z.string() })),
  previewHtml: z.string(),
  aiGenerated: z.boolean(),
});
export type GeneratedTemplate = z.infer<typeof generatedTemplateSchema>;

export function generateTemplate(input: {
  prompt: string;
  brandColor?: string;
  tone?: string;
  assetIds?: string[];
  variables?: string[];
}): Promise<GeneratedTemplate> {
  return api("/ai/templates/generate", { body: input, schema: generatedTemplateSchema });
}
