"use client";

import CodeMirror from "@uiw/react-codemirror";
import { html } from "@codemirror/lang-html";

export function MjmlSourceEditor({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="h-full overflow-auto">
      <CodeMirror
        value={value}
        height="100%"
        extensions={[html()]}
        onChange={onChange}
        basicSetup={{ lineNumbers: true, foldGutter: true }}
      />
    </div>
  );
}
