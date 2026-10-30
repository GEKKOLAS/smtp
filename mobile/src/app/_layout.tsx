import { isSignedOutError, loadCsrfToken } from "@/api/client";
import { queryKeys } from "@/api/endpoints";
import { useSession } from "@/hooks/data";
import { ThemeProvider, useAppFonts, useTheme } from "@/theme/theme";
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Stack } from "expo-router";
import * as SplashScreen from "expo-splash-screen";
import { StatusBar } from "expo-status-bar";
import { useEffect, useState } from "react";
import { SafeAreaProvider } from "react-native-safe-area-context";

void SplashScreen.preventAutoHideAsync();

// Any request that finds the session gone (expired, revoked on the web) drops the
// cached user, which flips the route guards below back to the sign-in screen.
const signOutOnAuthError = (error: unknown) => {
  if (isSignedOutError(error)) queryClient.setQueryData(queryKeys.me, null);
};

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: signOutOnAuthError }),
  mutationCache: new MutationCache({ onError: signOutOnAuthError }),
  defaultOptions: {
    queries: { retry: (count, error) => !isSignedOutError(error) && count < 2 },
  },
});

export default function RootLayout() {
  const [fontsLoaded, fontError] = useAppFonts();
  const [tokenLoaded, setTokenLoaded] = useState(false);

  useEffect(() => {
    loadCsrfToken().finally(() => setTokenLoaded(true));
  }, []);

  if ((!fontsLoaded && !fontError) || !tokenLoaded) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <SafeAreaProvider>
          <RootStack />
        </SafeAreaProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

function RootStack() {
  const { theme } = useTheme();
  const session = useSession();

  useEffect(() => {
    if (!session.isLoading) void SplashScreen.hideAsync();
  }, [session.isLoading]);

  if (session.isLoading) return null;
  const signedIn = !!session.data;

  return (
    <>
      <StatusBar style={theme.name === "dark" ? "light" : "dark"} />
      <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: theme.color.background } }}>
        <Stack.Protected guard={signedIn}>
          <Stack.Screen name="(tabs)" />
          <Stack.Screen name="message/[id]" />
          <Stack.Screen name="template/[id]" />
          <Stack.Screen name="reply/[id]" options={{ presentation: "fullScreenModal", animation: "slide_from_bottom" }} />
        </Stack.Protected>
        <Stack.Protected guard={!signedIn}>
          <Stack.Screen name="sign-in" />
        </Stack.Protected>
      </Stack>
    </>
  );
}
