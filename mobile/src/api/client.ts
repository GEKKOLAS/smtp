import { API_URL } from "@/lib/config";
import { storage } from "@/lib/storage";
import type { z } from "zod";

const CSRF_KEY = "mth.csrf";

/** RFC 7807 ProblemDetails fields the backend emits (docs/spec/06-api.md). */
type ProblemDetails = { title?: string; detail?: string; errorCode?: string };

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errorCode: string,
    message?: string,
  ) {
    super(message ?? errorCode);
    this.name = "ApiError";
  }
}

/** True when the server no longer recognises this device's session. */
export const isSignedOutError = (error: unknown) =>
  error instanceof ApiError && (error.status === 401 || error.errorCode === "csrf.invalid");

// The session itself is an HttpOnly cookie kept by the OS cookie store (fetch
// sends it with `credentials: "include"`). The CSRF double-submit value can't be
// read from that store, so login returns it in the body and we keep it here.
let csrfToken: string | null = null;

export async function loadCsrfToken() {
  csrfToken = await storage.get(CSRF_KEY);
}

export async function setCsrfToken(token: string | null) {
  csrfToken = token;
  if (token) await storage.set(CSRF_KEY, token);
  else await storage.remove(CSRF_KEY);
}

type RequestOptions<T> = {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
  schema?: z.ZodType<T>;
};

export async function api<T = unknown>(path: string, { method, body, schema }: RequestOptions<T> = {}): Promise<T> {
  const verb = method ?? (body === undefined ? "GET" : "POST");

  const response = await fetch(`${API_URL}${path}`, {
    method: verb,
    credentials: "include",
    headers: {
      Accept: "application/json",
      ...(body !== undefined && { "Content-Type": "application/json" }),
      ...(verb !== "GET" && csrfToken && { "X-CSRF-Token": csrfToken }),
    },
    ...(body !== undefined && { body: JSON.stringify(body) }),
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => undefined)) as ProblemDetails | undefined;
    throw new ApiError(response.status, problem?.errorCode ?? `http_${response.status}`, problem?.title ?? problem?.detail);
  }
  if (response.status === 204) return undefined as T;

  const json: unknown = await response.json();
  return schema ? schema.parse(json) : (json as T);
}
