import { getTemplate, previewTemplate, queryKeys } from "@/api/endpoints";
import { HtmlView } from "@/components/html-view";
import { Spinner, Txt } from "@/components/ui";
import { readableEmail } from "@/lib/email-html";
import { useTheme } from "@/theme/theme";
import { useQuery } from "@tanstack/react-query";
import { router, useLocalSearchParams } from "expo-router";
import { ChevronLeft } from "lucide-react-native";
import { Pressable, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

/** Full-screen preview of a template's current version, rendered with its sample values. */
export default function TemplatePreviewScreen() {
  const { theme: t } = useTheme();
  const { id } = useLocalSearchParams<{ id: string }>();
  const template = useQuery({ queryKey: queryKeys.template(id), queryFn: () => getTemplate(id) });
  const versionId = template.data?.currentVersion?.id ?? "";
  const preview = useQuery({
    queryKey: queryKeys.preview(versionId),
    queryFn: () => previewTemplate(versionId),
    enabled: !!versionId,
    staleTime: Infinity,
  });

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1 }}>
      <View style={{ paddingHorizontal: 12, paddingVertical: 8 }}>
        <Pressable
          accessibilityRole="button"
          onPress={() => router.back()}
          hitSlop={8}
          style={{ flexDirection: "row", alignItems: "center", gap: 2 }}
        >
          <ChevronLeft size={22} color={t.color.ink} />
          <Txt v="label" style={{ fontSize: 16 }}>
            Templates
          </Txt>
        </Pressable>
      </View>
      <View style={{ paddingHorizontal: 16, paddingBottom: 14, gap: 4 }}>
        <Txt v="h2" numberOfLines={2}>
          {template.data?.name ?? " "}
        </Txt>
        {template.data?.currentVersion && (
          <Txt v="mono" numberOfLines={1}>
            v{template.data.currentVersion.versionNumber} · {preview.data?.subject ?? template.data.currentVersion.subject}
          </Txt>
        )}
      </View>
      <View style={{ flex: 1, padding: 8, backgroundColor: t.color.raised, borderTopWidth: t.borderWidth, borderColor: t.color.border }}>
        {preview.data ? (
          <HtmlView html={readableEmail(preview.data.html)} style={{ flex: 1, borderRadius: 10 }} />
        ) : template.data && !versionId ? (
          <Txt style={{ padding: 16, color: t.color.muted }}>This template has no content yet.</Txt>
        ) : preview.isError || template.isError ? (
          <Txt style={{ padding: 16, color: t.color.danger }}>Could not render this template.</Txt>
        ) : (
          <Spinner />
        )}
      </View>
    </SafeAreaView>
  );
}
