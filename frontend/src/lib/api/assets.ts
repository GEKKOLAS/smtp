import { api, ApiError } from "@/lib/api/client";
import {
  type Asset,
  type AssetAccess,
  assetSchema,
  downloadUrlSchema,
  type PagedAssets,
  pagedAssetsSchema,
  uploadGrantSchema,
} from "@/lib/schemas/assets";

export interface ListAssetsParams {
  kind?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export function listAssets(params: ListAssetsParams = {}): Promise<PagedAssets> {
  const query = new URLSearchParams();
  if (params.kind) query.set("kind", params.kind);
  if (params.search) query.set("search", params.search);
  query.set("page", String(params.page ?? 1));
  query.set("pageSize", String(params.pageSize ?? 24));
  return api(`/assets?${query.toString()}`, { schema: pagedAssetsSchema });
}

export function getDownloadUrl(id: string): Promise<string> {
  return api(`/assets/${id}/download-url`, { schema: downloadUrlSchema }).then((r) => r.url);
}

export function setAssetVisibility(id: string, access: AssetAccess): Promise<Asset> {
  return api(`/assets/${id}/visibility`, { method: "POST", body: { access }, schema: assetSchema });
}

export function deleteAsset(id: string, force = false): Promise<void> {
  return api(`/assets/${id}${force ? "?force=true" : ""}`, { method: "DELETE" });
}

/**
 * Full upload: request a presigned grant, PUT the bytes straight to storage,
 * then confirm so the server verifies and finalizes the asset.
 */
export async function uploadFile(
  file: File,
  onProgress?: (fraction: number) => void,
): Promise<Asset> {
  const grant = await api("/assets/uploads", {
    body: { filename: file.name, mimeType: file.type, sizeBytes: file.size },
    schema: uploadGrantSchema,
  });

  await putToStorage(grant.uploadUrl, grant.headers, file, onProgress);

  return api(`/assets/uploads/${grant.assetId}/complete`, {
    method: "POST",
    schema: assetSchema,
  });
}

function putToStorage(
  url: string,
  headers: Record<string, string>,
  file: File,
  onProgress?: (fraction: number) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", url);
    for (const [name, value] of Object.entries(headers)) {
      xhr.setRequestHeader(name, value);
    }
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable && onProgress) onProgress(e.loaded / e.total);
    };
    xhr.onload = () =>
      xhr.status >= 200 && xhr.status < 300
        ? resolve()
        : reject(new ApiError(xhr.status, "asset.upload_failed", "Upload to storage failed."));
    xhr.onerror = () => reject(new ApiError(0, "asset.upload_failed", "Upload to storage failed."));
    xhr.send(file);
  });
}
