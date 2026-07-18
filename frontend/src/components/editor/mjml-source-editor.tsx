"use client";

import CodeMirror from "@uiw/react-codemirror";
import { html } from "@codemirror/lang-html";
import { oneDark } from "@codemirror/theme-one-dark";
import { EditorView } from "@codemirror/view";

const editorTheme = EditorView.theme({
  "&": { height: "100%", fontSize: "13px" },
  ".cm-scroller": { fontFamily: "var(--font-mono, monospace)" },
  ".cm-gutters": { backgroundColor: "transparent", border: "none" },
});

export function MjmlSourceEditor({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="h-full overflow-auto bg-[#0d1117]">
      <CodeMirror
        value={value}
        height="100%"
        theme={oneDark}
        extensions={[html(), editorTheme]}
        onChange={onChange}
        basicSetup={{ lineNumbers: true, foldGutter: true }}
      />
    </div>
  );
}
