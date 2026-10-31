import { describe, expect, it } from "vitest";
import type { BrandFont, BrandKit } from "@/lib/api/brand-kits";
import { brandBlocks, readableOn } from "@/lib/brand-blocks";

const kit: BrandKit = {
  id: "k1",
  name: 'Acme "Coffee" & Co',
  isDefault: true,
  colors: {
    primary: "#C2410C",
    secondary: "#00CEC9",
    background: "#FFF4E6",
    surface: "#FFFFFF",
    text: "#17151F",
    muted: "#6B6878",
    link: "#4B2FD6",
  },
  headingFont: "space-grotesk",
  bodyFont: "inter",
  logoAssetId: null,
  logoUrl: null,
  button: { radius: 6, style: "filled" },
  radius: { small: 4, medium: 8, large: 16 },
  spacing: { small: 8, medium: 16, large: 24 },
  voice: null,
  createdAt: "2026-10-30T00:00:00Z",
  updatedAt: "2026-10-30T00:00:00Z",
};

const fonts: BrandFont[] = [
  { key: "space-grotesk", label: "Space Grotesk", stack: "'Space Grotesk', Arial, sans-serif", googleFamily: "Space Grotesk" },
  { key: "inter", label: "Inter", stack: "Inter, Arial, sans-serif", googleFamily: "Inter" },
];

describe("brandBlocks", () => {
  it("writes the kit's values in literally", () => {
    const hero = brandBlocks(kit, fonts).find((b) => b.id === "brand-hero")!;
    expect(hero.content).toContain('background-color="#FFF4E6"');
    expect(hero.content).toContain("font-family=\"'Space Grotesk', Arial, sans-serif\"");
    expect(hero.content).toContain('border-radius="6px"');
    // White text reads better on the dark orange button.
    expect(hero.content).toContain('color="#FFFFFF"');
  });

  it("escapes the kit name inside markup", () => {
    const header = brandBlocks(kit, fonts).find((b) => b.id === "brand-header")!;
    expect(header.content).toContain("Acme &quot;Coffee&quot; &amp; Co");
    expect(header.content).not.toContain('"Coffee"');
  });

  it("uses the logo when the kit has one", () => {
    const blocks = brandBlocks({ ...kit, logoUrl: "https://cdn.example/logo.png" }, fonts);
    expect(blocks.find((b) => b.id === "brand-header")!.content).toContain('<mj-image src="https://cdn.example/logo.png"');
    expect(blocks.find((b) => b.id === "brand-footer")!.content).toContain("logo.png");
  });

  it("draws outline buttons without a fill", () => {
    const button = brandBlocks({ ...kit, button: { radius: 20, style: "outline" } }, fonts).find(
      (b) => b.id === "brand-button",
    )!;
    expect(button.content).toContain('background-color="transparent"');
    expect(button.content).toContain('border="2px solid #C2410C"');
    expect(button.content).toContain('border-radius="20px"');
  });
});

describe("readableOn", () => {
  it("picks ink for light colors and white for dark ones", () => {
    expect(readableOn("#C6F432")).toBe("#111111");
    expect(readableOn("#17151F")).toBe("#FFFFFF");
    expect(readableOn("not a color")).toBe("#111111");
  });
});
