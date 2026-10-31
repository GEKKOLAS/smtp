import { describe, expect, it } from "vitest";
import type { TemplateVariable, TemplateVersion } from "@/lib/schemas/templates";
import { diffFields, diffVariables, versionSource } from "@/lib/version-diff";

const version = (overrides: Partial<TemplateVersion>): TemplateVersion => ({
  id: "v",
  versionNumber: 1,
  subject: "Hi {{firstName}}",
  preheader: null,
  editorKind: "mjml",
  mjmlSource: "<mjml></mjml>",
  grapesProject: null,
  htmlBody: "<html></html>",
  textBody: null,
  variablesSchema: [],
  assets: [],
  createdAt: "2026-10-30T00:00:00Z",
  ...overrides,
});

const variable = (overrides: Partial<TemplateVariable>): TemplateVariable => ({
  name: "firstName",
  type: "text",
  required: false,
  default: null,
  sample: null,
  ...overrides,
});

describe("diffFields", () => {
  it("lists only the fields that changed, with empty values spelled out", () => {
    const changes = diffFields(
      version({ subject: "Hi", preheader: null }),
      version({ subject: "Hello", preheader: "Spring menu" }),
    );
    expect(changes).toEqual([
      { field: "Subject", before: "Hi", after: "Hello" },
      { field: "Preheader", before: "(empty)", after: "Spring menu" },
    ]);
  });

  it("treats null and empty as the same", () => {
    expect(diffFields(version({ textBody: null }), version({ textBody: "" }))).toEqual([]);
  });
});

describe("diffVariables", () => {
  it("finds added, removed and changed variables", () => {
    const changes = diffVariables(
      [variable({ name: "firstName" }), variable({ name: "city" }), variable({ name: "ctaUrl", type: "text" })],
      [
        variable({ name: "firstName" }),
        variable({ name: "ctaUrl", type: "url", required: true, default: "https://a.test" }),
        variable({ name: "coupon" }),
      ],
    );
    expect(changes).toEqual([
      { kind: "changed", name: "ctaUrl", details: ["type text → url", "default (empty) → https://a.test", "now required"] },
      { kind: "added", name: "coupon", variable: variable({ name: "coupon" }) },
      { kind: "removed", name: "city", variable: variable({ name: "city" }) },
    ]);
  });
});

describe("versionSource", () => {
  it("uses the HTML body for HTML templates and MJML otherwise", () => {
    expect(versionSource(version({ editorKind: "html", htmlBody: "<p>x</p>" }))).toBe("<p>x</p>");
    expect(versionSource(version({ editorKind: "visual", mjmlSource: "<mjml/>" }))).toBe("<mjml/>");
    expect(versionSource(version({ mjmlSource: null }))).toBe("");
  });
});
