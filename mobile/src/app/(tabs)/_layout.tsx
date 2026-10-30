import { useInbox, useMailbox } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import { Tabs, TabList, TabSlot, TabTrigger, type TabTriggerSlotProps } from "expo-router/ui";
import { Activity, Inbox, LayoutTemplate, type LucideIcon } from "lucide-react-native";
import { Pressable, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

// Design B: three thumb-reachable tabs, Letters first because on a phone you
// mostly answer mail. Building templates stays on the desktop app.
export default function TabsLayout() {
  const { theme: t } = useTheme();
  const insets = useSafeAreaInsets();
  const { mailbox } = useMailbox();
  const inbox = useInbox(mailbox?.id);
  const unread = inbox.data?.items.filter((m) => !m.isRead).length ?? 0;

  return (
    <Tabs style={{ flex: 1, backgroundColor: t.color.background }}>
      <TabSlot />
      <View
        style={{
          flexDirection: "row",
          justifyContent: "space-around",
          paddingTop: 8,
          paddingBottom: Math.max(insets.bottom, 10),
          backgroundColor: t.color.tabBar,
          borderTopWidth: t.borderWidth,
          borderTopColor: t.color.border,
        }}
      >
        <TabTrigger name="letters" asChild>
          <TabButton icon={Inbox} label="Letters" badge={unread} />
        </TabTrigger>
        <TabTrigger name="templates" asChild>
          <TabButton icon={LayoutTemplate} label="Templates" />
        </TabTrigger>
        <TabTrigger name="activity" asChild>
          <TabButton icon={Activity} label="Activity" />
        </TabTrigger>
      </View>
      <TabList style={{ display: "none" }}>
        <TabTrigger name="letters" href="/" />
        <TabTrigger name="templates" href="/templates" />
        <TabTrigger name="activity" href="/activity" />
      </TabList>
    </Tabs>
  );
}

// expo-router still types the slot's ref as RefAttributes<View>, which React Native 0.88's
// View types reject. Leave it out of the type; at runtime the spread below still forwards it.
type TabButtonProps = Omit<TabTriggerSlotProps, "ref"> & { icon: LucideIcon; label: string; badge?: number };

function TabButton({ icon: Icon, label, badge, isFocused, ...props }: TabButtonProps) {
  const { theme: t } = useTheme();
  return (
    <Pressable
      {...props}
      accessibilityRole="tab"
      accessibilityState={{ selected: isFocused }}
      accessibilityLabel={badge ? `${label}, ${badge} unread` : label}
      style={{ width: 84, alignItems: "center", gap: 4 }}
    >
      <View
        style={{
          width: 56,
          height: 32,
          borderRadius: 16,
          alignItems: "center",
          justifyContent: "center",
          backgroundColor: isFocused ? t.color.tabActive : "transparent",
        }}
      >
        <Icon size={20} strokeWidth={2.25} color={isFocused ? t.color.tabActiveInk : t.color.muted} />
      </View>
      {!!badge && (
        <View
          style={{
            position: "absolute",
            top: -4,
            left: 50,
            minWidth: 18,
            height: 18,
            paddingHorizontal: 5,
            borderRadius: 9,
            alignItems: "center",
            justifyContent: "center",
            backgroundColor: t.color.unread,
          }}
        >
          <Text style={{ fontFamily: t.mono.bold, fontSize: 10, color: "#111111" }}>{badge}</Text>
        </View>
      )}
      <Text style={{ fontFamily: t.font.semibold, fontSize: 11, color: isFocused ? t.color.ink : t.color.muted }}>
        {label}
      </Text>
    </Pressable>
  );
}
