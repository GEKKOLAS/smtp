import { logout, queryKeys } from "@/api/endpoints";
import type { Account, User } from "@/api/schemas";
import { Button, Txt } from "@/components/ui";
import { useTheme } from "@/theme/theme";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LogOut, Moon, Sun } from "lucide-react-native";
import { Modal, Pressable, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

const DOT: Record<Account["state"], string> = {
  active: "#4ADE80",
  needs_reconnect: "#FBBF24",
  revoked: "#F87171",
};

/** Bottom sheet behind the account chip: who you are, which inboxes are connected, theme and sign out. */
export function AccountSheet({
  open,
  onClose,
  user,
  accounts,
}: {
  open: boolean;
  onClose: () => void;
  user: User;
  accounts: Account[];
}) {
  const { theme: t, toggle } = useTheme();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const signOut = useMutation({
    mutationFn: logout,
    onSettled: () => {
      onClose();
      queryClient.clear();
      queryClient.setQueryData(queryKeys.me, null);
    },
  });

  return (
    <Modal visible={open} transparent animationType="slide" onRequestClose={onClose}>
      <Pressable accessibilityLabel="Close" onPress={onClose} style={{ flex: 1, backgroundColor: "rgba(10,10,15,0.45)" }} />
      <View
        style={{
          backgroundColor: t.color.card,
          borderTopLeftRadius: 24,
          borderTopRightRadius: 24,
          borderWidth: t.borderWidth,
          borderColor: t.color.border,
          padding: 20,
          paddingBottom: Math.max(insets.bottom, 20),
          gap: 16,
        }}
      >
        <View>
          <Txt v="title">{user.displayName}</Txt>
          <Txt v="mono">{user.email}</Txt>
        </View>
        {accounts.length > 0 && (
          <View style={{ gap: 10 }}>
            <Txt v="eyebrow">Sending from</Txt>
            {accounts.map((a) => (
              <View key={a.id} style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
                <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: DOT[a.state] }} />
                <Txt v="small" numberOfLines={1} style={{ flex: 1 }}>
                  {a.emailAddress}
                </Txt>
                {a.isDefault && <Txt v="mono">Default</Txt>}
              </View>
            ))}
            <Txt v="mono">Connect or reconnect accounts on the web app.</Txt>
          </View>
        )}
        <View style={{ gap: 10 }}>
          <Button
            kind="outline"
            icon={t.name === "dark" ? Sun : Moon}
            label={t.name === "dark" ? "Light theme" : "Dark theme"}
            onPress={toggle}
          />
          <Button kind="outline" icon={LogOut} label="Sign out" loading={signOut.isPending} onPress={() => signOut.mutate()} />
        </View>
      </View>
    </Modal>
  );
}
