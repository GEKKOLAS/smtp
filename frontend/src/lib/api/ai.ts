import { api } from "@/lib/api/client";
import { type Asset, assetSchema } from "@/lib/schemas/assets";
import { z } from "zod";

const generatedTemplateSchema = z.object({
  subject: z.string(),
  preheader: z.string().nullable(),
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
  videoUrl?: string;
  useAdvancedModel?: boolean;
  currentMjml?: string;
  currentHtml?: string;
  backgroundImageAssetId?: string;
  headerLogoAssetId?: string;
  footerLogoAssetId?: string;
  brandKitId?: string;
}): Promise<GeneratedTemplate> {
  return api("/ai/templates/generate", { body: input, schema: generatedTemplateSchema });
}

export type CopyKind = "subject" | "preheader" | "body" | "cta";
export type CopyTone = "promotional" | "professional" | "technical" | "followUp";

const copyVariationsSchema = z.object({
  variations: z.array(z.object({ text: z.string(), note: z.string().nullable() })),
});
export type CopyVariation = z.infer<typeof copyVariationsSchema>["variations"][number];

/** Tone-based rewrites; the server drops any that lose or invent a {{variable}}. */
export function rewriteCopy(input: {
  kind: CopyKind;
  text: string;
  tone: CopyTone;
  count?: number;
  brandKitId?: string;
  context?: string;
}): Promise<CopyVariation[]> {
  return api("/ai/copy/variations", { body: input, schema: copyVariationsSchema }).then((r) => r.variations);
}

const aiCapabilitiesSchema = z.object({ imageGeneration: z.boolean(), copyAssistant: z.boolean() });
export type AiCapabilities = z.infer<typeof aiCapabilitiesSchema>;

export function getAiCapabilities(): Promise<AiCapabilities> {
  return api("/ai/capabilities", { schema: aiCapabilitiesSchema });
}

export type ImageSize = "1024x1024" | "1536x1024" | "1024x1536";

/** Generates an image from a prompt; the result is saved as a ready, public asset. */
export function generateImage(input: { prompt: string; size?: ImageSize }): Promise<Asset> {
  return api("/ai/images/generate", { body: input, schema: assetSchema });
}
