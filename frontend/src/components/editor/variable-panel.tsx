"use client";

import type { TemplateVariable } from "@/lib/schemas/templates";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/**
 * Lists the variables detected in the content and lets the author set the sample
 * values used for preview. Detection is a simple {{name}} scan.
 */
export function VariablePanel({
  detected,
  variables,
  onChange,
}: {
  detected: string[];
  variables: TemplateVariable[];
  onChange: (next: TemplateVariable[]) => void;
}) {
  const byName = new Map(variables.map((v) => [v.name, v]));
  const merged: TemplateVariable[] = detected.map(
    (name) => byName.get(name) ?? { name, type: "text", required: true, default: null, sample: "" },
  );

  function update(name: string, patch: Partial<TemplateVariable>) {
    const next = merged.map((v) => (v.name === name ? { ...v, ...patch } : v));
    onChange(next);
  }

  if (merged.length === 0) {
    return (
      <p className="p-3 text-sm text-muted-foreground">
        No variables yet. Add <code>{"{{firstName}}"}</code> anywhere to personalize.
      </p>
    );
  }

  return (
    <div className="space-y-4 p-3">
      {merged.map((variable) => (
        <div key={variable.name} className="space-y-1.5">
          <div className="flex items-center justify-between">
            <Label className="font-mono text-xs">{`{{${variable.name}}}`}</Label>
            <select
              value={variable.type}
              onChange={(e) => update(variable.name, { type: e.target.value as TemplateVariable["type"] })}
              className="rounded border bg-background px-1.5 py-0.5 text-xs"
            >
              <option value="text">text</option>
              <option value="url">url</option>
              <option value="html">html</option>
            </select>
          </div>
          <Input
            value={variable.sample ?? ""}
            placeholder="Sample value for preview"
            onChange={(e) => update(variable.name, { sample: e.target.value })}
            className="h-8 text-sm"
          />
          <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <input
              type="checkbox"
              checked={variable.required}
              onChange={(e) => update(variable.name, { required: e.target.checked })}
            />
            Required
          </label>
        </div>
      ))}
    </div>
  );
}

/** Extracts distinct {{variable}} names from any text. */
export function detectVariables(...sources: (string | null | undefined)[]): string[] {
  const names = new Set<string>();
  const re = /\{\{\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\}\}/g;
  for (const source of sources) {
    if (!source) continue;
    for (const match of source.matchAll(re)) names.add(match[1]);
  }
  return [...names];
}
