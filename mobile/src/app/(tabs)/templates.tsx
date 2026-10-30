import { listTemplates, queryKeys } from "@/api/endpoints";
import { TemplateThumb } from "@/components/template-thumb";
import { TopBar } from "@/components/top-bar";
import { Card, Spinner, Txt } from "@/components/ui";
import { useTheme } from "@/theme/theme";
import { useQuery } from "@tanstack/react-query";
import { router } from "expo-router";
import { Monitor } from "lucide-react-native";
import { Pressable, RefreshControl, ScrollView, useWindowDimensions, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

const GAP = 12;

/** Templates — browse what you can reply with. Designing them stays on the desktop app. */
export default function TemplatesScreen() {
  const { theme: t } = useTheme();
  const { width } = useWindowDimensions();
  const templates = useQuery({ queryKey: queryKeys.templates, queryFn: listTemplates });
  const cardWidth = Math.floor((Math.min(width, 640) - 32 - GAP) / 2);

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1 }}>
      <ScrollView
        contentContainerStyle={{ padding: 16, gap: 18 }}
        refreshControl={
          <RefreshControl refreshing={templates.isRefetching} onRefresh={() => void templates.refetch()} tintColor={t.color.ink} />
        }
      >
        <TopBar />
        <View style={{ marginTop: 6 }}>
          <Txt v="eyebrow">{templates.data ? `${templates.data.totalCount} active` : "Library"}</Txt>
          <Txt v="h1" style={{ marginTop: 6 }}>
            Templates
          </Txt>
        </View>

        <View
          style={{
            flexDirection: "row",
            alignItems: "center",
            gap: 10,
            padding: 12,
            borderRadius: 12,
            borderWidth: t.borderWidth,
            borderStyle: t.name === "light" ? "dashed" : "solid",
            borderColor: t.color.border,
          }}
        >
          <Monitor size={18} color={t.color.muted} />
          <Txt v="small" style={{ flex: 1, color: t.color.muted }}>
            Design and edit templates on the desktop app. Here you can preview them and reply with them.
          </Txt>
        </View>

        {templates.isLoading && <Spinner />}
        {templates.isError && <Txt style={{ color: t.color.danger }}>Could not load templates.</Txt>}

        {templates.data && (
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: GAP }}>
            {templates.data.items.map((item, i) => (
              <Pressable
                key={item.id}
                accessibilityRole="button"
                accessibilityLabel={`Preview ${item.name}`}
                onPress={() => router.push({ pathname: "/template/[id]", params: { id: item.id } })}
                style={{ width: cardWidth, transform: [{ rotate: t.tilt(i % 2 ? 0.6 : -0.6) }] }}
              >
                <Card raised style={{ overflow: "hidden" }}>
                  <TemplateThumb versionId={item.currentVersionId} width={cardWidth - 2 * t.borderWidth} height={Math.round(cardWidth * 1.15)} />
                  <View style={{ padding: 10, borderTopWidth: t.borderWidth, borderTopColor: t.color.border, gap: 2 }}>
                    <Txt v="label" numberOfLines={1}>
                      {item.name}
                    </Txt>
                    <Txt v="mono" numberOfLines={1}>
                      {item.currentVersionNumber ? `v${item.currentVersionNumber}` : "draft"}
                    </Txt>
                  </View>
                </Card>
              </Pressable>
            ))}
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
