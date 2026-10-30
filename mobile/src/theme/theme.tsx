import { storage } from "@/lib/storage";
import { STAMP_TONES, themes, type Theme, type ThemeName } from "@/theme/tokens";
import { Geist_400Regular } from "@expo-google-fonts/geist/400Regular";
import { Geist_500Medium } from "@expo-google-fonts/geist/500Medium";
import { Geist_600SemiBold } from "@expo-google-fonts/geist/600SemiBold";
import { Geist_700Bold } from "@expo-google-fonts/geist/700Bold";
import { GeistMono_400Regular } from "@expo-google-fonts/geist-mono/400Regular";
import { GeistMono_500Medium } from "@expo-google-fonts/geist-mono/500Medium";
import { SpaceGrotesk_400Regular } from "@expo-google-fonts/space-grotesk/400Regular";
import { SpaceGrotesk_500Medium } from "@expo-google-fonts/space-grotesk/500Medium";
import { SpaceGrotesk_600SemiBold } from "@expo-google-fonts/space-grotesk/600SemiBold";
import { SpaceGrotesk_700Bold } from "@expo-google-fonts/space-grotesk/700Bold";
import { SpaceMono_400Regular } from "@expo-google-fonts/space-mono/400Regular";
import { SpaceMono_700Bold } from "@expo-google-fonts/space-mono/700Bold";
import { useFonts } from "expo-font";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useColorScheme } from "react-native";

const THEME_KEY = "mth.theme";

export const FONTS = {
  Geist_400Regular,
  Geist_500Medium,
  Geist_600SemiBold,
  Geist_700Bold,
  GeistMono_400Regular,
  GeistMono_500Medium,
  SpaceGrotesk_400Regular,
  SpaceGrotesk_500Medium,
  SpaceGrotesk_600SemiBold,
  SpaceGrotesk_700Bold,
  SpaceMono_400Regular,
  SpaceMono_700Bold,
};

export const useAppFonts = () => useFonts(FONTS);

type ThemeContextValue = { theme: Theme; toggle: () => void };

const ThemeContext = createContext<ThemeContextValue | null>(null);

/** Follows the system scheme until the user picks one with the ☀/☾ button; that choice is remembered. */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const system = useColorScheme();
  const [override, setOverride] = useState<ThemeName | null>(null);

  useEffect(() => {
    storage.get(THEME_KEY).then((saved) => {
      if (saved === "light" || saved === "dark") setOverride(saved);
    });
  }, []);

  const name: ThemeName = override ?? (system === "dark" ? "dark" : "light");
  const toggle = () => {
    const next = name === "dark" ? "light" : "dark";
    setOverride(next);
    void storage.set(THEME_KEY, next);
  };

  return <ThemeContext.Provider value={{ theme: themes[name], toggle }}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const value = useContext(ThemeContext);
  if (!value) throw new Error("useTheme must be used inside ThemeProvider");
  return value;
}

/** Same sender, same color — a cheap stable hash keeps the list from shuffling hues on refetch. */
export function stampTone(theme: Theme, name: string) {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  const tones = STAMP_TONES[theme.name];
  return tones[Math.abs(hash) % tones.length];
}

export function initialsOf(name: string) {
  const parts = name.replace(/@.*/, "").split(/[\s._-]+/).filter(Boolean);
  return ((parts[0]?.[0] ?? "") + (parts[1]?.[0] ?? "")).toUpperCase() || "?";
}
