import { ApiError } from "@/api/client";
import { login, queryKeys } from "@/api/endpoints";
import { Button, Card, Txt } from "@/components/ui";
import { API_URL } from "@/lib/config";
import { useTheme } from "@/theme/theme";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Mail } from "lucide-react-native";
import { useState } from "react";
import { KeyboardAvoidingView, Platform, ScrollView, TextInput, View, type TextInputProps } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

function errorCopy(error: unknown) {
  if (!(error instanceof ApiError)) return `Can't reach the server at ${API_URL}. Is the app running on your computer?`;
  if (error.status === 429) return "Too many attempts. Wait a minute and try again.";
  if (error.status === 401 || error.status === 400) return "That email and password don't match.";
  return "Something went wrong signing in. Try again.";
}

export default function SignInScreen() {
  const { theme: t } = useTheme();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const signIn = useMutation({
    mutationFn: () => login(email.trim(), password),
    // Setting the user flips the route guard in the root layout to the tabs.
    onSuccess: (user) => queryClient.setQueryData(queryKeys.me, user),
  });

  const field: TextInputProps["style"] = {
    height: 48,
    paddingHorizontal: 14,
    borderRadius: 12,
    borderWidth: t.borderWidth,
    borderColor: t.name === "light" ? t.color.border : "rgba(255,255,255,0.12)",
    backgroundColor: t.name === "light" ? t.color.card : t.color.raised,
    color: t.color.ink,
    fontFamily: t.font.regular,
    fontSize: 16,
  };

  return (
    <SafeAreaView style={{ flex: 1 }}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === "ios" ? "padding" : undefined}>
        <ScrollView contentContainerStyle={{ flexGrow: 1, justifyContent: "center", padding: 20, gap: 24 }} keyboardShouldPersistTaps="handled">
          <View style={{ alignItems: "center", gap: 14 }}>
            <View
              style={{
                width: 56,
                height: 56,
                borderRadius: t.name === "light" ? 6 : 16,
                alignItems: "center",
                justifyContent: "center",
                backgroundColor: t.color.primary,
                borderWidth: t.name === "light" ? 2 : 0,
                borderColor: t.color.border,
                boxShadow: t.shadow.md,
                transform: [{ rotate: t.tilt(-4) }],
              }}
            >
              <Mail size={26} strokeWidth={2.25} color={t.color.onPrimary} />
            </View>
            <Txt v="h1" style={{ textAlign: "center" }}>
              Mail Template Hub
            </Txt>
            <Txt style={{ textAlign: "center", color: t.color.muted }}>Answer your letters with your templates.</Txt>
          </View>

          <Card raised style={{ padding: 18, gap: 14 }}>
            <View style={{ gap: 6 }}>
              <Txt v="label">Email</Txt>
              <TextInput
                value={email}
                onChangeText={setEmail}
                autoCapitalize="none"
                autoComplete="email"
                keyboardType="email-address"
                textContentType="username"
                accessibilityLabel="Email"
                style={field}
              />
            </View>
            <View style={{ gap: 6 }}>
              <Txt v="label">Password</Txt>
              <TextInput
                value={password}
                onChangeText={setPassword}
                secureTextEntry
                autoComplete="current-password"
                textContentType="password"
                accessibilityLabel="Password"
                returnKeyType="go"
                onSubmitEditing={() => signIn.mutate()}
                style={field}
              />
            </View>
            {signIn.isError && <Txt style={{ color: t.color.danger }}>{errorCopy(signIn.error)}</Txt>}
            <Button
              size="lg"
              label="Sign in"
              loading={signIn.isPending}
              disabled={!email.trim() || !password}
              onPress={() => signIn.mutate()}
            />
          </Card>

          <View style={{ gap: 4 }}>
            <Txt v="mono" style={{ textAlign: "center" }}>
              No account or forgot your password? Use the web app.
            </Txt>
            <Txt v="mono" style={{ textAlign: "center", fontSize: 11 }}>
              Server: {API_URL}
            </Txt>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
