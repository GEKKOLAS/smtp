"use client";

import { uploadFile } from "@/lib/api/assets";
import { ApiError } from "@/lib/api/client";
import { ACCEPTED_MIME_TYPES } from "@/lib/schemas/assets";
import { useRef, useState } from "react";
import { toast } from "sonner";

interface UploadItem {
  id: string;
  name: string;
  progress: number;
  status: "uploading" | "done" | "error";
  error?: string;
}

export function UploadDropzone({ onUploaded }: { onUploaded: () => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [items, setItems] = useState<UploadItem[]>([]);

  async function upload(files: FileList | File[]) {
    for (const file of Array.from(files)) {
      const id = crypto.randomUUID();
      setItems((prev) => [...prev, { id, name: file.name, progress: 0, status: "uploading" }]);
      try {
        await uploadFile(file, (fraction) =>
          setItems((prev) => prev.map((i) => (i.id === id ? { ...i, progress: fraction } : i))),
        );
        setItems((prev) => prev.map((i) => (i.id === id ? { ...i, progress: 1, status: "done" } : i)));
        onUploaded();
      } catch (error) {
        const message =
          error instanceof ApiError
            ? messageForError(error)
            : "Upload failed. Please try again.";
        setItems((prev) => prev.map((i) => (i.id === id ? { ...i, status: "error", error: message } : i)));
        toast.error(`${file.name}: ${message}`);
      }
    }
    // Clear finished rows shortly after.
    setTimeout(() => setItems((prev) => prev.filter((i) => i.status === "uploading")), 2500);
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          if (e.dataTransfer.files.length) void upload(e.dataTransfer.files);
        }}
        className={`flex w-full flex-col items-center justify-center rounded-lg border-2 border-dashed px-6 py-10 text-center transition-colors ${
          dragging ? "border-primary bg-primary/5" : "border-muted-foreground/25 hover:border-muted-foreground/50"
        }`}
      >
        <p className="text-sm font-medium">Drop files here or click to upload</p>
        <p className="mt-1 text-xs text-muted-foreground">
          Images &amp; GIFs up to 10 MB · documents up to 25 MB
        </p>
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={ACCEPTED_MIME_TYPES.join(",")}
          className="hidden"
          onChange={(e) => {
            if (e.target.files?.length) void upload(e.target.files);
            e.target.value = "";
          }}
        />
      </button>

      {items.length > 0 && (
        <ul className="mt-3 space-y-2">
          {items.map((item) => (
            <li key={item.id} className="rounded-md border px-3 py-2 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="truncate">{item.name}</span>
                <span className="shrink-0 text-xs text-muted-foreground">
                  {item.status === "done" ? "Done" : item.status === "error" ? "Failed" : `${Math.round(item.progress * 100)}%`}
                </span>
              </div>
              <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                <div
                  className={`h-full transition-all ${item.status === "error" ? "bg-destructive" : "bg-primary"}`}
                  style={{ width: `${Math.max(4, item.progress * 100)}%` }}
                />
              </div>
              {item.error && <p className="mt-1 text-xs text-destructive">{item.error}</p>}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function messageForError(error: ApiError): string {
  switch (error.errorCode) {
    case "asset.type_not_allowed":
      return "This file type is not allowed.";
    case "asset.too_large":
      return "This file exceeds the size limit.";
    case "asset.quota_exceeded":
      return "You have reached your storage quota.";
    case "asset.verification_failed":
      return "The file content did not match its type.";
    default:
      return error.message ?? "Upload failed.";
  }
}
