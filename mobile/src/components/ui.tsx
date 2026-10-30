import type { SendStatus } from "@/api/schemas";
import { initialsOf, stampTone, useTheme } from "@/theme/theme";
import type { Theme } from "@/theme/tokens";
import type { LucideIcon } from "lucide-react-native";
import type { ReactNode } from "react";
import {
  ActivityIndicator,
  Pressable,
  Text,
  View,
  type PressableProps,
  type StyleProp,
  type TextProps,
  type TextStyle,
  type ViewStyle,
} from "react-native";

type Variant = "eyebrow" | "h1" | "h2" | "title" | "body" | "small" | "mono" | "label";

function textStyle(t: Theme, variant: Variant): TextStyle {
  const light = t.name === "light";
  switch (variant) {
    case "eyebrow":
      return {
        fontFamily: light ? t.mono.bold : t.mono.regular,
        fontSize: 12,
        letterSpacing: 0.6,
        textTransform: "uppercase",
        color: light ? t.color.ink : t.color.muted,
      };
    case "h1":
      return {
        fontFamily: light ? t.font.bold : t.font.semibold,
        fontSize: light ? 38 : 30,
        lineHeight: light ? 40 : 34,
        letterSpacing: light ? -1.5 : -0.75,
        color: t.color.ink,
      };
    case "h2":
      return {
        fontFamily: light ? t.font.bold : t.font.semibold,
        fontSize: light ? 20 : 16,
        letterSpacing: -0.2,
        color: t.color.ink,
      };
    case "title":
      return { fontFamily: light ? t.font.bold : t.font.semibold, fontSize: 16, color: t.color.ink };
    case "body":
      return { fontFamily: t.font.regular, fontSize: 15, lineHeight: 21, color: t.color.ink };
    case "small":
      return { fontFamily: t.font.medium, fontSize: 13, color: t.color.ink };
    case "label":
      return { fontFamily: t.font.semibold, fontSize: 14, color: t.color.ink };
    case "mono":
      return { fontFamily: t.mono.regular, fontSize: 12, color: t.color.muted };
  }
}

export function Txt({ v = "body", style, ...props }: TextProps & { v?: Variant }) {
  const { theme } = useTheme();
  return <Text {...props} style={[textStyle(theme, v), style]} />;
}

/** A surface with the theme's border (and Mailroom's hard shadow when `raised`). */
export function Card({
  children,
  raised,
  style,
}: {
  children: ReactNode;
  raised?: boolean;
  style?: StyleProp<ViewStyle>;
}) {
  const { theme } = useTheme();
  return (
    <View
      style={[
        {
          backgroundColor: theme.color.card,
          borderColor: theme.color.border,
          borderWidth: theme.borderWidth,
          borderRadius: 16,
          boxShadow: raised ? theme.shadow.sm : undefined,
        },
        style,
      ]}
    >
      {children}
    </View>
  );
}

type ButtonKind = "primary" | "outline" | "ghost";

export function Button({
  kind = "primary",
  icon: Icon,
  label,
  loading,
  size = "md",
  style,
  ...props
}: Omit<PressableProps, "style" | "children"> & {
  kind?: ButtonKind;
  icon?: LucideIcon;
  label: string;
  loading?: boolean;
  size?: "md" | "lg";
  style?: StyleProp<ViewStyle>;
}) {
  const { theme: t } = useTheme();
  const disabled = props.disabled || loading;
  const fg = kind === "primary" ? t.color.onPrimary : kind === "ghost" ? t.color.ink : t.color.ink;
  return (
    <Pressable
      accessibilityRole="button"
      {...props}
      disabled={disabled}
      style={({ pressed }) => [
        {
          height: size === "lg" ? 52 : 40,
          paddingHorizontal: 16,
          borderRadius: size === "lg" ? 14 : 10,
          flexDirection: "row",
          alignItems: "center",
          justifyContent: "center",
          gap: 8,
          opacity: disabled ? 0.5 : 1,
        },
        kind === "primary" && {
          backgroundColor: t.color.primary,
          borderWidth: t.name === "light" ? 2 : 0,
          borderColor: t.color.border,
          boxShadow: t.name === "light" ? t.shadow.sm : "0px 8px 24px -12px #9B87FF",
        },
        kind === "outline" && {
          backgroundColor: t.color.card,
          borderWidth: t.borderWidth,
          borderColor: t.color.border,
          boxShadow: t.shadow.sm,
        },
        pressed && { transform: [{ translateY: 1 }], opacity: 0.85 },
        style,
      ]}
    >
      {loading ? <ActivityIndicator color={fg} /> : Icon && <Icon size={size === "lg" ? 19 : 16} color={fg} strokeWidth={2.25} />}
      <Text
        style={{
          fontFamily: t.name === "light" ? t.font.bold : t.font.semibold,
          fontSize: size === "lg" ? 17 : 15,
          color: kind === "ghost" ? t.color.link : fg,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
}

export function IconButton({
  icon: Icon,
  label,
  ...props
}: Omit<PressableProps, "children" | "style"> & { icon: LucideIcon; label: string }) {
  const { theme: t } = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      hitSlop={6}
      {...props}
      style={({ pressed }) => ({
        width: 40,
        height: 40,
        borderRadius: 10,
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: t.color.card,
        borderWidth: t.borderWidth,
        borderColor: t.color.border,
        boxShadow: t.shadow.sm,
        opacity: pressed ? 0.8 : 1,
      })}
    >
      <Icon size={18} color={t.color.ink} strokeWidth={2.25} />
    </Pressable>
  );
}

export function Chip({
  label,
  on,
  icon: Icon,
  onPress,
}: {
  label: string;
  on?: boolean;
  icon?: LucideIcon;
  onPress: () => void;
}) {
  const { theme: t } = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: on }}
      onPress={onPress}
      style={{
        height: 34,
        paddingHorizontal: 14,
        borderRadius: 17,
        flexDirection: "row",
        alignItems: "center",
        gap: 6,
        borderWidth: t.borderWidth,
        borderColor: on && t.name === "dark" ? "transparent" : t.color.border,
        backgroundColor: on ? t.color.primary : t.color.card,
      }}
    >
      {Icon && <Icon size={14} color={on ? t.color.onPrimary : t.color.ink} strokeWidth={2.25} />}
      <Text style={{ fontFamily: t.font.semibold, fontSize: 13, color: on ? t.color.onPrimary : t.color.ink }}>
        {label}
      </Text>
    </Pressable>
  );
}

/** Sender initials as a postage stamp (Mailroom) or a soft tile (Nocturne). */
export function Stamp({ name, size = 40, tilt = 2.5 }: { name: string; size?: number; tilt?: number }) {
  const { theme: t } = useTheme();
  const tone = stampTone(t, name);
  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={{
        width: size,
        height: Math.round(size * 1.15),
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: tone.bg,
        borderRadius: t.stamp.radius,
        borderWidth: t.stamp.dashed ? 2 : 0,
        borderStyle: "dashed",
        borderColor: "#111111",
        transform: [{ rotate: t.tilt(tilt) }],
      }}
    >
      <Text style={{ fontFamily: t.font.bold, fontSize: size * 0.33, color: tone.ink }}>{initialsOf(name)}</Text>
    </View>
  );
}

const STATUS_COPY: Record<SendStatus, { label: string; tone: "success" | "info" | "warning" | "danger" | "muted" }> = {
  scheduled: { label: "Scheduled", tone: "muted" },
  queued: { label: "Queued", tone: "info" },
  sending: { label: "Sending", tone: "info" },
  retrying: { label: "Retrying", tone: "warning" },
  sent: { label: "Sent", tone: "success" },
  partiallyfailed: { label: "Partly failed", tone: "warning" },
  failed: { label: "Failed", tone: "danger" },
  cancelled: { label: "Cancelled", tone: "muted" },
};

export function StatusBadge({ status }: { status: SendStatus }) {
  const { theme: t } = useTheme();
  const copy = STATUS_COPY[status];
  const ink = {
    success: t.color.success,
    info: t.color.info,
    warning: t.color.warning,
    danger: t.color.danger,
    muted: t.color.muted,
  }[copy.tone];
  const soft = {
    success: t.color.successSoft,
    info: t.color.infoSoft,
    warning: t.color.warningSoft,
    danger: t.name === "dark" ? "rgba(248,113,113,0.12)" : "transparent",
    muted: t.name === "dark" ? t.color.raised : "transparent",
  }[copy.tone];
  return (
    <View
      style={{
        paddingHorizontal: 8,
        height: 22,
        justifyContent: "center",
        borderRadius: t.loud ? 6 : 11,
        borderWidth: t.loud ? 2 : 0,
        borderColor: ink,
        backgroundColor: soft,
        transform: [{ rotate: t.tilt(-2) }],
      }}
    >
      <Text
        style={{
          fontFamily: t.loud ? t.mono.bold : t.font.medium,
          fontSize: t.loud ? 11 : 12,
          letterSpacing: t.loud ? 0.4 : 0,
          textTransform: t.loud ? "uppercase" : "none",
          color: ink,
        }}
      >
        {copy.label}
      </Text>
    </View>
  );
}

export function Spinner() {
  const { theme } = useTheme();
  return <ActivityIndicator color={theme.color.ink} style={{ padding: 24 }} />;
}
