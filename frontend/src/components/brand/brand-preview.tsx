import { type BrandFont, type BrandKitInput, fontStack } from "@/lib/api/brand-kits";
import { readableOn } from "@/lib/brand-blocks";

function googleFontsHref(families: string[]): string | null {
  const unique = [...new Set(families)];
  if (unique.length === 0) return null;
  const params = unique.map((f) => `family=${encodeURIComponent(f).replace(/%20/g, "+")}:wght@400;700`).join("&");
  return `https://fonts.googleapis.com/css2?${params}&display=swap`;
}

/**
 * A small email drawn with the kit's own values, so every change shows up
 * immediately. It is plain HTML/CSS, not the MJML render: a quick taste of the
 * brand, not a send preview.
 */
export function BrandPreview({
  kit,
  logoUrl,
  fonts,
}: {
  kit: BrandKitInput;
  logoUrl: string | null;
  fonts: BrandFont[] | undefined;
}) {
  const { colors } = kit;
  const heading = fontStack(fonts, kit.headingFont);
  const body = fontStack(fonts, kit.bodyFont);
  const webFonts = [kit.headingFont, kit.bodyFont]
    .map((key) => fonts?.find((f) => f.key === key)?.googleFamily)
    .filter((family): family is string => Boolean(family));
  const fontsHref = googleFontsHref(webFonts);
  const filled = kit.button.style === "filled";

  return (
    <div
      className="overflow-hidden rounded-xl border-2 dark:border"
      style={{ background: colors.background, padding: kit.spacing.large }}
      aria-label="Brand preview"
    >
      {fontsHref && <link rel="stylesheet" href={fontsHref} precedence="default" />}
      <div
        className="mx-auto max-w-md"
        style={{
          background: colors.surface,
          borderRadius: kit.radius.large,
          padding: kit.spacing.large,
          color: colors.text,
          fontFamily: body,
        }}
      >
        <div style={{ marginBottom: kit.spacing.medium }}>
          {logoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element -- arbitrary user asset host
            <img src={logoUrl} alt="" className="h-9 w-auto max-w-40 object-contain" />
          ) : (
            <span style={{ fontFamily: heading, fontWeight: 700, color: colors.primary }}>{kit.name}</span>
          )}
        </div>
        <p
          style={{ fontFamily: heading, fontWeight: 700, fontSize: 24, lineHeight: 1.2, margin: 0 }}
        >
          Your next campaign starts here
        </p>
        <p style={{ marginTop: kit.spacing.small, fontSize: 14, lineHeight: 1.6 }}>
          This is how body copy reads in your brand. Questions? Read the{" "}
          <span style={{ color: colors.link, textDecoration: "underline" }}>full guide</span>.
        </p>
        <div
          style={{
            marginTop: kit.spacing.medium,
            background: colors.background,
            borderRadius: kit.radius.medium,
            padding: kit.spacing.medium,
            fontSize: 13,
          }}
        >
          <span style={{ color: colors.secondary, fontWeight: 700 }}>●</span> A card on the background color
        </div>
        <span
          className="mt-5 inline-block px-5 py-2.5 text-sm font-bold"
          style={{
            marginTop: kit.spacing.large,
            borderRadius: kit.button.radius,
            fontFamily: body,
            background: filled ? colors.primary : "transparent",
            color: filled ? readableOn(colors.primary) : colors.primary,
            border: `2px solid ${colors.primary}`,
          }}
        >
          Call to action
        </span>
        <p style={{ marginTop: kit.spacing.large, fontSize: 12, color: colors.muted }}>
          You get this email because you asked for it.
        </p>
      </div>
    </div>
  );
}
