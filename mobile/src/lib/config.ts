import Constants from "expo-constants";

/**
 * Where the API lives. `EXPO_PUBLIC_API_URL` wins (set it in `.env.local` for a
 * staging or production backend). In development the default is the Next.js dev
 * server on the computer running Metro: it already proxies `/api` to the .NET
 * backend, and a phone on the same Wi-Fi can reach it by that machine's LAN IP.
 */
function devApiUrl() {
  const host = Constants.expoConfig?.hostUri?.split(":")[0] ?? "localhost";
  return `http://${host}:3000/api/v1`;
}

export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? devApiUrl()).replace(/\/$/, "");
