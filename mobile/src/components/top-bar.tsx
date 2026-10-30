import { AccountSheet } from "@/components/account-sheet";
import { IconButton } from "@/components/ui";
import { useMailbox, useSession } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import { ChevronDown, Moon, Sun } from "lucide-react-native";
import { useState } from "react";
import { Pressable, Text, View } from "react-native";

/** The account chip (opens the account sheet) and the ☀/☾ toggle, shown above every tab. */
export function TopBar() {
  const { theme: t, toggle } = useTheme();
  const { data: user } = useSession();
  const { accounts, mailbox } = useMailbox();
  const [open, setOpen] = useState(false);
  const shown = mailbox ?? accounts.data?.find((a) => a.isDefault) ?? accounts.data?.[0];

  return (
    <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Account menu"
        onPress={() => setOpen(true)}
        style={{
          flexShrink: 1,
          height: 36,
          paddingHorizontal: 14,
          borderRadius: 18,
          flexDirection: "row",
          alignItems: "center",
          gap: 8,
          backgroundColor: t.color.card,
          borderWidth: t.borderWidth,
          borderColor: t.color.border,
        }}
      >
        <View
          style={{
            width: 8,
            height: 8,
            borderRadius: 4,
            backgroundColor: shown?.state === "active" ? "#4ADE80" : shown ? "#FBBF24" : t.color.muted,
          }}
        />
        <Text numberOfLines={1} style={{ flexShrink: 1, fontFamily: t.font.semibold, fontSize: 13, color: t.color.ink }}>
          {shown?.emailAddress ?? user?.displayName ?? "Account"}
        </Text>
        <ChevronDown size={14} color={t.color.muted} />
      </Pressable>
      <IconButton icon={t.name === "dark" ? Sun : Moon} label="Toggle theme" onPress={toggle} />
      {user && <AccountSheet open={open} onClose={() => setOpen(false)} user={user} accounts={accounts.data ?? []} />}
    </View>
  );
}
