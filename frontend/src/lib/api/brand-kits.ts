import { api } from "@/lib/api/client";
import { z } from "zod";

const colorsSchema = z.object({
  primary: z.string(),
  secondary: z.string(),
  background: z.string(),
  surface: z.string(),
  text: z.string(),
  muted: z.string(),
  link: z.string(),
});
export type BrandColors = z.infer<typeof colorsSchema>;
export type BrandColorName = keyof BrandColors;

const scaleSchema = z.object({ small: z.number(), medium: z.number(), large: z.number() });
export type BrandScale = z.infer<typeof scaleSchema>;

export const brandKitSchema = z.object({
  id: z.string(),
  name: z.string(),
  isDefault: z.boolean(),
  colors: colorsSchema,
  headingFont: z.string(),
  bodyFont: z.string(),
  logoAssetId: z.string().nullable(),
  logoUrl: z.string().nullable(),
  button: z.object({ radius: z.number(), style: z.enum(["filled", "outline"]) }),
  radius: scaleSchema,
  spacing: scaleSchema,
  voice: z.string().nullable(),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type BrandKit = z.infer<typeof brandKitSchema>;

/** What the editor sends: the kit minus server-owned fields. */
export type BrandKitInput = Omit<BrandKit, "id" | "isDefault" | "logoUrl" | "createdAt" | "updatedAt">;

export const brandFontSchema = z.object({
  key: z.string(),
  label: z.string(),
  stack: z.string(),
  googleFamily: z.string().nullable(),
});
export type BrandFont = z.infer<typeof brandFontSchema>;

const listSchema = z.object({ items: z.array(brandKitSchema) });
const fontsSchema = z.object({ items: z.array(brandFontSchema) });

export function listBrandKits(): Promise<BrandKit[]> {
  return api("/brand-kits", { schema: listSchema }).then((r) => r.items);
}

export function listBrandFonts(): Promise<BrandFont[]> {
  return api("/brand-kits/fonts", { schema: fontsSchema }).then((r) => r.items);
}

export function createBrandKit(input: BrandKitInput): Promise<BrandKit> {
  return api("/brand-kits", { body: input, schema: brandKitSchema });
}

export function updateBrandKit(id: string, input: BrandKitInput): Promise<BrandKit> {
  return api(`/brand-kits/${id}`, { method: "PUT", body: input, schema: brandKitSchema });
}

export function setDefaultBrandKit(id: string): Promise<BrandKit> {
  return api(`/brand-kits/${id}/default`, { method: "POST", schema: brandKitSchema });
}

export function deleteBrandKit(id: string): Promise<void> {
  return api(`/brand-kits/${id}`, { method: "DELETE" });
}

/** Same-origin download link; the session cookie authorizes it. */
export function brandTokensUrl(id: string, format: "json" | "css"): string {
  return `/api/v1/brand-kits/${id}/tokens?format=${format}`;
}

export function toBrandKitInput(kit: BrandKit): BrandKitInput {
  return {
    name: kit.name,
    colors: kit.colors,
    headingFont: kit.headingFont,
    bodyFont: kit.bodyFont,
    logoAssetId: kit.logoAssetId,
    button: kit.button,
    radius: kit.radius,
    spacing: kit.spacing,
    voice: kit.voice,
  };
}

/** A neutral starting point for a new kit (the user is expected to make it theirs). */
export const STARTER_KIT: BrandKitInput = {
  name: "My brand",
  colors: {
    primary: "#6C5CE7",
    secondary: "#00CEC9",
    background: "#F4F2FB",
    surface: "#FFFFFF",
    text: "#17151F",
    muted: "#6B6878",
    link: "#4B2FD6",
  },
  headingFont: "space-grotesk",
  bodyFont: "inter",
  logoAssetId: null,
  button: { radius: 10, style: "filled" },
  radius: { small: 6, medium: 12, large: 20 },
  spacing: { small: 8, medium: 16, large: 24 },
  voice: null,
};

export function fontStack(fonts: BrandFont[] | undefined, key: string): string {
  return fonts?.find((f) => f.key === key)?.stack ?? "Arial, Helvetica, sans-serif";
}
