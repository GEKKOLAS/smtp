import type { TemplateVariable, TemplateVersion } from "@/lib/schemas/templates";

export type FieldChange = { field: string; before: string; after: string };

export type VariableChange =
  | { kind: "added"; name: string; variable: TemplateVariable }
  | { kind: "removed"; name: string; variable: TemplateVariable }
  | { kind: "changed"; name: string; details: string[] };

const show = (value: string | null | undefined) => (value === null || value === undefined || value === "" ? "(empty)" : value);

/** The text a version's markup lives in: MJML source, or the HTML body for HTML templates. */
export function versionSource(version: TemplateVersion): string {
  return version.editorKind === "html" ? version.htmlBody : (version.mjmlSource ?? "");
}

/** Subject, preheader, editor kind and plain-text body that differ between two versions. */
export function diffFields(before: TemplateVersion, after: TemplateVersion): FieldChange[] {
  const fields: [string, string | null, string | null][] = [
    ["Subject", before.subject, after.subject],
    ["Preheader", before.preheader, after.preheader],
    ["Editor", before.editorKind, after.editorKind],
    ["Plain-text body", before.textBody, after.textBody],
  ];
  return fields
    .filter(([, a, b]) => (a ?? "") !== (b ?? ""))
    .map(([field, a, b]) => ({ field, before: show(a), after: show(b) }));
}

/** Variables added, removed, or changed in type, default or required. */
export function diffVariables(before: TemplateVariable[], after: TemplateVariable[]): VariableChange[] {
  const old = new Map(before.map((v) => [v.name, v]));
  const now = new Map(after.map((v) => [v.name, v]));
  const changes: VariableChange[] = [];

  for (const [name, variable] of now) {
    const previous = old.get(name);
    if (!previous) {
      changes.push({ kind: "added", name, variable });
      continue;
    }
    const details: string[] = [];
    if (previous.type !== variable.type) details.push(`type ${previous.type} → ${variable.type}`);
    if ((previous.default ?? "") !== (variable.default ?? "")) {
      details.push(`default ${show(previous.default)} → ${show(variable.default)}`);
    }
    if (previous.required !== variable.required) {
      details.push(variable.required ? "now required" : "no longer required");
    }
    if (details.length > 0) changes.push({ kind: "changed", name, details });
  }
  for (const [name, variable] of old) {
    if (!now.has(name)) changes.push({ kind: "removed", name, variable });
  }
  return changes;
}
