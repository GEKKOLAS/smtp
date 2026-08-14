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
}): Promise<GeneratedTemplate> {
  return api("/ai/templates/generate", { body: input, schema: generatedTemplateSchema });
}

const aiCapabilitiesSchema = z.object({ imageGeneration: z.boolean() });
export type AiCapabilities = z.infer<typeof aiCapabilitiesSchema>;

export function getAiCapabilities(): Promise<AiCapabilities> {
  return api("/ai/capabilities", { schema: aiCapabilitiesSchema });
}

export type ImageSize = "1024x1024" | "1536x1024" | "1024x1536";

/** Generates an image from a prompt; the result is saved as a ready, public asset. */
export function generateImage(input: { prompt: string; size?: ImageSize }): Promise<Asset> {
  return api("/ai/images/generate", { body: input, schema: assetSchema });
}
