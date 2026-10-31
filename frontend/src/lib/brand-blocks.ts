import { type BrandFont, type BrandKit, fontStack } from "@/lib/api/brand-kits";

export type BrandBlock = { id: string; label: string; content: string; media: string };

function escapeHtml(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function swatch(color: string, shape: string): string {
  return `<svg viewBox="0 0 24 24" width="28" height="28" aria-hidden="true">${shape.replaceAll("{c}", color)}</svg>`;
}

/**
 * MJML blocks for the GrapesJS "Brand" category (spec 14 F1). Every value is
 * written in literally, so a dropped block doesn't depend on the kit afterwards:
 * editing the kit later never restyles templates or sent history.
 */
export function brandBlocks(kit: BrandKit, fonts: BrandFont[] | undefined): BrandBlock[] {
  const { colors } = kit;
  const heading = escapeHtml(fontStack(fonts, kit.headingFont));
  const body = escapeHtml(fontStack(fonts, kit.bodyFont));
  const name = escapeHtml(kit.name);
  const logo = kit.logoUrl ? escapeHtml(kit.logoUrl) : null;
  const button =
    kit.button.style === "outline"
      ? `background-color="transparent" color="${colors.primary}" border="2px solid ${colors.primary}"`
      : `background-color="${colors.primary}" color="${readableOn(colors.primary)}"`;

  const ctaButton = (align: string) =>
    `<mj-button ${button} border-radius="${kit.button.radius}px" font-family="${body}" font-weight="700" ` +
    `inner-padding="14px 28px" align="${align}" href="https://">Call to action</mj-button>`;

  return [
    {
      id: "brand-header",
      label: "Header",
      media: swatch(colors.primary, '<rect x="2" y="5" width="20" height="5" rx="1.5" fill="{c}"/><rect x="2" y="13" width="12" height="2" rx="1" fill="currentColor" opacity=".4"/>'),
      content:
        `<mj-section background-color="${colors.surface}" padding="20px 24px">` +
        "<mj-column>" +
        (logo
          ? `<mj-image src="${logo}" alt="${name}" width="120px" align="left" padding="0" />`
          : `<mj-text font-family="${heading}" font-size="20px" font-weight="700" color="${colors.primary}" padding="0">${name}</mj-text>`) +
        "</mj-column></mj-section>",
    },
    {
      id: "brand-hero",
      label: "Hero",
      media: swatch(colors.primary, '<rect x="2" y="3" width="20" height="18" rx="2" fill="none" stroke="currentColor" stroke-width="1.5"/><rect x="5" y="7" width="12" height="2.5" rx="1" fill="currentColor"/><rect x="5" y="14" width="7" height="3.5" rx="1.5" fill="{c}"/>'),
      content:
        `<mj-section background-color="${colors.background}" padding="${kit.spacing.large * 2}px 24px">` +
        "<mj-column>" +
        `<mj-text font-family="${heading}" font-size="30px" font-weight="700" line-height="1.2" color="${colors.text}">Your headline goes here</mj-text>` +
        `<mj-text font-family="${body}" font-size="15px" line-height="1.6" color="${colors.text}">One or two sentences that tell the reader why this email matters to them.</mj-text>` +
        ctaButton("left") +
        "</mj-column></mj-section>",
    },
    {
      id: "brand-card",
      label: "Card",
      media: swatch(colors.secondary, '<rect x="3" y="4" width="18" height="16" rx="3" fill="{c}" opacity=".35"/><rect x="6" y="8" width="9" height="2" rx="1" fill="currentColor"/><rect x="6" y="12" width="12" height="1.5" rx=".75" fill="currentColor" opacity=".5"/>'),
      content:
        `<mj-section background-color="${colors.background}" padding="${kit.spacing.medium}px 24px">` +
        `<mj-column background-color="${colors.surface}" border-radius="${kit.radius.large}px" padding="${kit.spacing.large}px">` +
        `<mj-text font-family="${heading}" font-size="18px" font-weight="700" color="${colors.text}">Card title</mj-text>` +
        `<mj-text font-family="${body}" font-size="14px" line-height="1.6" color="${colors.muted}">A short paragraph for a feature, an offer or an update.</mj-text>` +
        "</mj-column></mj-section>",
    },
    {
      id: "brand-button",
      label: "Button",
      media: swatch(colors.primary, '<rect x="2" y="8" width="20" height="8" rx="4" fill="{c}"/>'),
      content: ctaButton("center"),
    },
    {
      id: "brand-footer",
      label: "Footer",
      media: swatch(colors.muted, '<rect x="2" y="15" width="20" height="5" rx="1.5" fill="{c}"/><rect x="7" y="9" width="10" height="2" rx="1" fill="currentColor" opacity=".4"/>'),
      content:
        `<mj-section background-color="${colors.background}" padding="24px">` +
        "<mj-column>" +
        (logo ? `<mj-image src="${logo}" alt="${name}" width="64px" padding="0 0 8px" />` : "") +
        `<mj-text align="center" font-family="${body}" font-size="12px" line-height="1.6" color="${colors.muted}">${name} · You get this email because you asked for it.</mj-text>` +
        "</mj-column></mj-section>",
    },
  ];
}

/** #111111 or #FFFFFF, whichever reads better on the given #RRGGBB background. */
export function readableOn(hex: string): string {
  const value = Number.parseInt(hex.replace("#", ""), 16);
  if (Number.isNaN(value)) return "#111111";
  const [r, g, b] = [(value >> 16) & 255, (value >> 8) & 255, value & 255].map((channel) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.4 ? "#111111" : "#FFFFFF";
}
