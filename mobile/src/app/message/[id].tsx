import { getInboxMessage, queryKeys } from "@/api/endpoints";
import { HtmlView } from "@/components/html-view";
import { Button, Spinner, Stamp, Txt } from "@/components/ui";
import { formatMailTime } from "@/hooks/data";
import { readableEmail } from "@/lib/email-html";
import { useTheme } from "@/theme/theme";
import { useQuery } from "@tanstack/react-query";
import { router, useLocalSearchParams } from "expo-router";
import { ChevronLeft, CornerUpLeft } from "lucide-react-native";
import { Pressable, ScrollView, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

export default function MessageScreen() {
  const { theme: t } = useTheme();
  const { id, accountId } = useLocalSearchParams<{ id: string; accountId: string }>();
  const message = useQuery({
    queryKey: queryKeys.inboxMessage(accountId, id),
    queryFn: () => getInboxMessage(accountId, id),
  });
  const data = message.data;
  const sender = data ? (data.fromName ?? data.fromEmail) : "";

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1 }}>
      <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", paddingHorizontal: 12, paddingVertical: 8 }}>
        <Pressable
          accessibilityRole="button"
          onPress={() => router.back()}
          hitSlop={8}
          style={{ flexDirection: "row", alignItems: "center", gap: 2 }}
        >
          <ChevronLeft size={22} color={t.color.ink} />
          <Txt v="label" style={{ fontSize: 16 }}>
            Letters
          </Txt>
        </Pressable>
        {data && (
          <Button
            icon={CornerUpLeft}
            label="Reply"
            onPress={() => router.push({ pathname: "/reply/[id]", params: { id, accountId } })}
          />
        )}
      </View>

      {message.isLoading && <Spinner />}
      {message.isError && (
        <Txt style={{ padding: 16, color: t.color.danger }}>Could not load this letter.</Txt>
      )}

      {data && (
        <View style={{ flex: 1 }}>
          <View style={{ paddingHorizontal: 16, paddingBottom: 14, gap: 14 }}>
            <Txt v="h2" style={{ fontSize: t.name === "light" ? 24 : 20, lineHeight: t.name === "light" ? 28 : 25 }}>
              {data.subject || "(no subject)"}
            </Txt>
            <View style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
              <Stamp name={sender} size={36} tilt={-3} />
              <View style={{ flex: 1 }}>
                <Txt v="label" numberOfLines={1}>
                  {sender}
                </Txt>
                <Txt v="mono" numberOfLines={1}>
                  {data.fromEmail}
                </Txt>
              </View>
              <Txt v="mono">{formatMailTime(data.receivedAt)}</Txt>
            </View>
          </View>
          <View style={{ flex: 1, padding: 8, backgroundColor: t.color.raised, borderTopWidth: t.borderWidth, borderColor: t.color.border }}>
            {data.html ? (
              <HtmlView html={readableEmail(data.html)} style={{ flex: 1, borderRadius: 10 }} />
            ) : (
              <ScrollView style={{ flex: 1, borderRadius: 10, backgroundColor: t.color.card }} contentContainerStyle={{ padding: 16 }}>
                <Txt selectable>{data.text ?? "(no content)"}</Txt>
              </ScrollView>
            )}
          </View>
        </View>
      )}
    </SafeAreaView>
  );
}
