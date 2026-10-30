// The two themes of the web app (frontend/src/app/globals.css), as React Native tokens.
// Light is "Mailroom": lilac paper, 2px ink borders, hard offset shadows, Space Grotesk/Mono.
// Dark is "Nocturne": near-black, hairline borders, violet accent, Geist/Geist Mono.

export type ThemeName = "light" | "dark";

export type Theme = {
  name: ThemeName;
  color: {
    background: string;
    card: string;
    raised: string;
    ink: string;
    muted: string;
    border: string;
    hairline: string;
    primary: string;
    onPrimary: string;
    link: string;
    unread: string;
    danger: string;
    tabBar: string;
    tabActive: string;
    tabActiveInk: string;
    success: string;
    successSoft: string;
    info: string;
    infoSoft: string;
    warning: string;
    warningSoft: string;
  };
  /** 2 in Mailroom, 1 in Nocturne. */
  borderWidth: number;
  /** Mailroom's hard offset shadows; Nocturne has none. */
  shadow: { sm: string | undefined; md: string | undefined };
  font: { regular: string; medium: string; semibold: string; bold: string };
  mono: { regular: string; bold: string };
  /** Mailroom tilts stamps and badges a little; Nocturne keeps everything straight. */
  tilt: (deg: number) => string;
  /** Avatars: tilted dashed stamps in Mailroom, soft rounded tiles in Nocturne. */
  stamp: { radius: number; dashed: boolean };
  /** Mono labels shout in Mailroom (bold, uppercase badges). */
  loud: boolean;
};

export const STAMP_TONES = {
  light: [
    { bg: "#C9BDFF", ink: "#111111" },
    { bg: "#9FD8FF", ink: "#111111" },
    { bg: "#FFD23F", ink: "#111111" },
    { bg: "#FF9ED2", ink: "#111111" },
  ],
  dark: [
    { bg: "#2A2440", ink: "#D4C8FF" },
    { bg: "#1E3047", ink: "#BFDBFE" },
    { bg: "#23232F", ink: "#B4B4C6" },
    { bg: "#3A2A1E", ink: "#FCD9B6" },
  ],
};

export const themes: Record<ThemeName, Theme> = {
  light: {
    name: "light",
    color: {
      background: "#E7E2FF",
      card: "#FFFFFF",
      raised: "#F1EDFF",
      ink: "#111111",
      muted: "#55525F",
      border: "#111111",
      hairline: "#111111",
      primary: "#C6F432",
      onPrimary: "#111111",
      link: "#4B2FD6",
      unread: "#FF7A45",
      danger: "#C2410C",
      tabBar: "#FFFFFF",
      tabActive: "#C6F432",
      tabActiveInk: "#111111",
      success: "#0B6B3A",
      successSoft: "transparent",
      info: "#4B2FD6",
      infoSoft: "transparent",
      warning: "#8A5300",
      warningSoft: "transparent",
    },
    borderWidth: 2,
    shadow: { sm: "3px 3px 0px #111111", md: "4px 4px 0px #111111" },
    font: {
      regular: "SpaceGrotesk_400Regular",
      medium: "SpaceGrotesk_500Medium",
      semibold: "SpaceGrotesk_600SemiBold",
      bold: "SpaceGrotesk_700Bold",
    },
    mono: { regular: "SpaceMono_400Regular", bold: "SpaceMono_700Bold" },
    tilt: (deg) => `${deg}deg`,
    stamp: { radius: 3, dashed: true },
    loud: true,
  },
  dark: {
    name: "dark",
    color: {
      background: "#0A0A0F",
      card: "#13131B",
      raised: "#1A1A24",
      ink: "#EDEDF3",
      muted: "#8F8FA3",
      border: "rgba(255,255,255,0.08)",
      hairline: "rgba(255,255,255,0.06)",
      primary: "#9B87FF",
      onPrimary: "#0A0A0F",
      link: "#B8A8FF",
      unread: "#9B87FF",
      danger: "#F87171",
      tabBar: "#0E0E14",
      tabActive: "#1A1A24",
      tabActiveInk: "#B8A8FF",
      success: "#86EFAC",
      successSoft: "rgba(74,222,128,0.1)",
      info: "#A9CBFA",
      infoSoft: "rgba(93,156,242,0.12)",
      warning: "#FCD34D",
      warningSoft: "rgba(251,191,36,0.12)",
    },
    borderWidth: 1,
    shadow: { sm: undefined, md: undefined },
    font: {
      regular: "Geist_400Regular",
      medium: "Geist_500Medium",
      semibold: "Geist_600SemiBold",
      bold: "Geist_700Bold",
    },
    mono: { regular: "GeistMono_400Regular", bold: "GeistMono_500Medium" },
    tilt: () => "0deg",
    stamp: { radius: 12, dashed: false },
    loud: false,
  },
};
