import type { EditorKind, TemplateContentInput } from "@/lib/schemas/templates";

export const STARTER_MJML = `<mjml>
  <mj-body background-color="#f4f4f4">
    <mj-section background-color="#ffffff" padding="24px">
      <mj-column>
        <mj-text font-size="20px" font-weight="bold">Hello {{firstName}}</mj-text>
        <mj-text color="#555555" line-height="1.5">
          Write your message here. Drag blocks from the panel to build your email.
        </mj-text>
        <mj-button background-color="#2563eb" href="{{ctaUrl}}">Get started</mj-button>
      </mj-column>
    </mj-section>
  </mj-body>
</mjml>`;

export const STARTER_HTML = `<div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
  <h1>Hello {{firstName}}</h1>
  <p>Write your message here.</p>
</div>`;

/** A blank content payload for a newly created template. */
export function starterContent(editorKind: EditorKind): TemplateContentInput {
  return {
    editorKind,
    subject: "Hello {{firstName}}",
    preheader: null,
    mjmlSource: editorKind === "html" ? null : STARTER_MJML,
    grapesProject: null,
    htmlBody: editorKind === "html" ? STARTER_HTML : "",
    textBody: null,
    variables: [
      { name: "firstName", type: "text", required: true, default: null, sample: "Ada" },
    ],
    assets: [],
  };
}
