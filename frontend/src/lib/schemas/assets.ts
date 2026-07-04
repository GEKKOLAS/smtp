import { z } from "zod";

export const assetKindSchema = z.enum(["image", "gif", "document", "archive", "other"]);
export type AssetKind = z.infer<typeof assetKindSchema>;

export const assetAccessSchema = z.enum(["private", "public"]);
export type AssetAccess = z.infer<typeof assetAccessSchema>;

export const assetSchema = z.object({
  id: z.string(),
  kind: assetKindSchema,
  originalFilename: z.string(),
  mimeType: z.string(),
  sizeBytes: z.number(),
  access: assetAccessSchema,
  publicUrl: z.string().nullable(),
  width: z.number().nullable(),
  height: z.number().nullable(),
  checksumSha256: z.string().nullable(),
  createdAt: z.string(),
});
export type Asset = z.infer<typeof assetSchema>;

export const pagedAssetsSchema = z.object({
  items: z.array(assetSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});
export type PagedAssets = z.infer<typeof pagedAssetsSchema>;

export const uploadGrantSchema = z.object({
  assetId: z.string(),
  uploadUrl: z.string(),
  headers: z.record(z.string(), z.string()),
  expiresAt: z.string(),
});

export const downloadUrlSchema = z.object({ url: z.string(), expiresAt: z.string() });

// Allowlist mirrors the backend (docs/spec/01-prd US-AST-1).
export const ACCEPTED_MIME_TYPES = [
  "image/png",
  "image/jpeg",
  "image/webp",
  "image/gif",
  "application/pdf",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  "text/plain",
  "text/csv",
  "application/zip",
];

export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
export const MAX_FILE_BYTES = 25 * 1024 * 1024;
