import { useSyncExternalStore } from "react";

/** Live `matchMedia` result. Reports `false` on the server, where there is no viewport. */
export function useMediaQuery(query: string) {
  return useSyncExternalStore(
    (onChange) => {
      const mql = window.matchMedia(query);
      mql.addEventListener("change", onChange);
      return () => mql.removeEventListener("change", onChange);
    },
    () => window.matchMedia(query).matches,
    () => false,
  );
}

/** The `lg` breakpoint: at and above it the app shows the sidebar shell, below it the phone shell. */
export const DESKTOP_QUERY = "(min-width: 1024px)";

export const useIsDesktop = () => useMediaQuery(DESKTOP_QUERY);

/** Where a signed-in user lands: phones open on their letters, desktops on the dashboard. */
export function homePath() {
  return window.matchMedia(DESKTOP_QUERY).matches ? "/dashboard" : "/inbox";
}
