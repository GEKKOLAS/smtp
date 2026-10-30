import { previewTemplate, queryKeys } from "@/api/endpoints";
import { HtmlView } from "@/components/html-view";
import { Txt } from "@/components/ui";
import { thumbnailEmail } from "@/lib/email-html";
import { useQuery } from "@tanstack/react-query";
import { View } from "react-native";

/** The real rendered template (sample variables), shrunk to a card. */
export function TemplateThumb({ versionId, width, height }: { versionId: string | null; width: number; height: number }) {
  const preview = useQuery({
    queryKey: queryKeys.preview(versionId ?? ""),
    queryFn: () => previewTemplate(versionId!),
    enabled: !!versionId,
    staleTime: Infinity, // versions are immutable
    retry: false,
  });

  return (
    <View style={{ width, height, backgroundColor: "#FFFFFF", overflow: "hidden" }}>
      {preview.data ? (
        <HtmlView html={thumbnailEmail(preview.data.html, width)} interactive={false} style={{ width, height }} />
      ) : (
        <View style={{ flex: 1, alignItems: "center", justifyContent: "center" }}>
          <Txt v="mono" style={{ color: "#55525F" }}>
            {!versionId ? "No content" : preview.isError ? "No preview" : "Loading…"}
          </Txt>
        </View>
      )}
    </View>
  );
}
