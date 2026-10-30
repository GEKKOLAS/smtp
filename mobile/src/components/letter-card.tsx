import type { InboxMessage } from "@/api/schemas";
import { Stamp, Txt } from "@/components/ui";
import { formatMailTime } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import * as Haptics from "expo-haptics";
import { CornerUpLeft } from "lucide-react-native";
import { useEffect, useMemo, useState } from "react";
import { Animated, PanResponder, Platform, Pressable, Text, View } from "react-native";

const ACTION_WIDTH = 76;

/**
 * One inbox message as a letter. Tap opens it; swipe left reveals Reply.
 * `revealed` is owned by the list so only one letter is open at a time.
 */
export function LetterCard({
  message,
  revealed,
  onReveal,
  onOpen,
  onReply,
}: {
  message: InboxMessage;
  revealed: boolean;
  onReveal: (revealed: boolean) => void;
  onOpen: () => void;
  onReply: () => void;
}) {
  const { theme: t } = useTheme();
  const sender = message.fromName ?? message.fromEmail;
  const [x] = useState(() => new Animated.Value(0));
  const base = revealed ? -ACTION_WIDTH : 0;

  useEffect(() => {
    Animated.spring(x, { toValue: base, useNativeDriver: Platform.OS !== "web", bounciness: 4 }).start();
  }, [base, x]);

  const pan = useMemo(() => {
    const settle = () => Animated.spring(x, { toValue: base, useNativeDriver: Platform.OS !== "web" }).start();
    return PanResponder.create({
      // Capture phase: the Pressable inside is already the responder from the touch start, so a
      // bubbling handler would never see the drag. Claim only clearly horizontal drags so
      // vertical scrolling keeps working.
      onMoveShouldSetPanResponderCapture: (_, g) => Math.abs(g.dx) > 8 && Math.abs(g.dx) > Math.abs(g.dy) * 1.5,
      onPanResponderTerminationRequest: () => false,
      onPanResponderMove: (_, g) => x.setValue(Math.min(0, Math.max(-ACTION_WIDTH * 1.25, base + g.dx))),
      onPanResponderRelease: (_, g) => {
        const open = base + g.dx < -ACTION_WIDTH / 2;
        if (open === revealed) return settle();
        if (open && Platform.OS !== "web") void Haptics.selectionAsync();
        onReveal(open);
      },
      onPanResponderTerminate: settle,
    });
  }, [base, onReveal, revealed, x]);

  return (
    <View>
      <View style={{ position: "absolute", top: 4, bottom: 4, right: 0, width: ACTION_WIDTH - 8 }}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Reply to ${sender}`}
          accessibilityElementsHidden={!revealed}
          importantForAccessibility={revealed ? "auto" : "no-hide-descendants"}
          onPress={() => {
            onReveal(false);
            onReply();
          }}
          style={{
            flex: 1,
            borderRadius: 16,
            alignItems: "center",
            justifyContent: "center",
            gap: 4,
            backgroundColor: t.color.primary,
            borderWidth: t.name === "light" ? 2 : 0,
            borderColor: t.color.border,
          }}
        >
          <CornerUpLeft size={18} strokeWidth={2.25} color={t.color.onPrimary} />
          <Text style={{ fontFamily: t.font.bold, fontSize: 11, color: t.color.onPrimary }}>Reply</Text>
        </Pressable>
      </View>

      <Animated.View style={{ transform: [{ translateX: x }] }} {...pan.panHandlers}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`${message.isRead ? "" : "Unread. "}${sender}: ${message.subject}`}
          accessibilityHint="Opens the letter. Swipe left to reply."
          onPress={() => (revealed ? onReveal(false) : onOpen())}
          style={{
            paddingVertical: 14,
            paddingLeft: 16,
            paddingRight: 66,
            borderRadius: 16,
            backgroundColor: t.color.card,
            borderWidth: t.borderWidth,
            borderColor: t.color.border,
            boxShadow: message.isRead ? undefined : t.shadow.sm,
          }}
        >
          <View style={{ flexDirection: "row", alignItems: "baseline", gap: 8 }}>
            <Txt
              numberOfLines={1}
              style={{ flexShrink: 1, fontSize: 16, fontFamily: message.isRead ? t.font.medium : t.font.bold }}
            >
              {sender}
            </Txt>
            <Txt v="mono" style={{ fontSize: 11 }}>
              {formatMailTime(message.receivedAt)}
            </Txt>
          </View>
          <Txt
            numberOfLines={1}
            style={{
              marginTop: 2,
              fontSize: 14,
              fontFamily: message.isRead ? t.font.regular : t.font.semibold,
              color: message.isRead ? t.color.muted : t.color.ink,
            }}
          >
            {message.subject || "(no subject)"}
          </Txt>
          {!!message.snippet && (
            <Txt v="mono" numberOfLines={1} style={{ marginTop: 4 }}>
              {message.snippet}
            </Txt>
          )}
          <View style={{ position: "absolute", top: 12, right: 12 }}>
            <Stamp name={sender} />
          </View>
        </Pressable>
      </Animated.View>
    </View>
  );
}
