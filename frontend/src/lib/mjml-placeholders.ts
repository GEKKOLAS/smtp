export type PlaceholderRole = "headerLogo" | "background" | "footerLogo";

const MARKERS: Record<PlaceholderRole, string> = {
  headerLogo: "mth-header-logo",
  background: "mth-background-hero",
  footerLogo: "mth-footer-logo",
};

function blockRegex(marker: string): RegExp {
  return new RegExp(`<mj-section[^>]*css-class="${marker}"[^>]*>[\\s\\S]*?<\\/mj-section>`);
}

function buildBlock(role: PlaceholderRole, url: string): string {
  switch (role) {
    case "headerLogo":
      return `<mj-section css-class="${MARKERS.headerLogo}" padding="16px 0"><mj-column><mj-image src="${url}" width="140px" align="center" alt="Logo" /></mj-column></mj-section>`;
    case "footerLogo":
      return `<mj-section css-class="${MARKERS.footerLogo}" padding="16px 0"><mj-column><mj-image src="${url}" width="100px" align="center" alt="Logo" /></mj-column></mj-section>`;
    case "background":
      return `<mj-section css-class="${MARKERS.background}" background-url="${url}" background-size="cover" background-repeat="no-repeat" padding="48px 24px"><mj-column></mj-column></mj-section>`;
  }
}

/**
 * Inserts, replaces, or removes one of the three manual image "slots" in an
 * MJML document, tracked by a dedicated css-class marker so re-applying a
 * different image (or clearing it) always finds the same block again.
 */
export function applyPlaceholder(mjmlSource: string, role: PlaceholderRole, imageUrl: string | null): string {
  const existing = mjmlSource.match(blockRegex(MARKERS[role]));

  if (imageUrl === null) {
    return existing ? mjmlSource.replace(existing[0], "") : mjmlSource;
  }

  const block = buildBlock(role, imageUrl);
  if (existing) {
    return mjmlSource.replace(existing[0], block);
  }

  if (role === "footerLogo") {
    return mjmlSource.replace(/<\/mj-body>/, `${block}</mj-body>`);
  }
  if (role === "background") {
    const header = mjmlSource.match(blockRegex(MARKERS.headerLogo));
    if (header) return mjmlSource.replace(header[0], `${header[0]}${block}`);
  }
  // headerLogo, or background with no header logo yet: right after <mj-body ...>.
  return mjmlSource.replace(/(<mj-body[^>]*>)/, `$1${block}`);
}

/** Reads back the current image URL (if any) for each of the three slots. */
export function detectPlaceholders(mjmlSource: string): Record<PlaceholderRole, string | null> {
  const extract = (role: PlaceholderRole, attr: "src" | "background-url"): string | null => {
    const block = mjmlSource.match(blockRegex(MARKERS[role]));
    if (!block) return null;
    const m = block[0].match(new RegExp(`${attr}="([^"]*)"`));
    return m ? m[1] : null;
  };
  return {
    headerLogo: extract("headerLogo", "src"),
    background: extract("background", "background-url"),
    footerLogo: extract("footerLogo", "src"),
  };
}
