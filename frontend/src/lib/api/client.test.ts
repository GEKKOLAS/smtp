import { afterEach, describe, expect, it, vi } from "vitest";

import { api, ApiError, problemDetailsSchema } from "./client";

describe("problemDetailsSchema", () => {
  it("parses a backend ProblemDetails payload", () => {
    const parsed = problemDetailsSchema.parse({
      title: "An unexpected error occurred.",
      status: 500,
      errorCode: "internal_error",
      traceId: "abc123",
    });

    expect(parsed.errorCode).toBe("internal_error");
  });

  it("tolerates unknown-shaped bodies", () => {
    expect(problemDetailsSchema.safeParse({ weird: true }).success).toBe(true);
  });
});

describe("api", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("returns parsed JSON on success", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ ok: true }), { status: 200 }),
      ),
    );

    await expect(api<{ ok: boolean }>("/ping")).resolves.toEqual({ ok: true });
  });

  it("throws a typed ApiError carrying the backend errorCode", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({ title: "Conflict", errorCode: "template.name_taken" }),
          { status: 409 },
        ),
      ),
    );

    const error = await api("/templates", { body: { name: "x" } }).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).errorCode).toBe("template.name_taken");
  });
});
