import { isActiveStatus, listSends, queryKeys } from "@/api/endpoints";
import { groupByDay, SendRow } from "@/components/send-row";
import { TopBar } from "@/components/top-bar";
import { Card, Spinner, Txt } from "@/components/ui";
import { useTheme } from "@/theme/theme";
import { useQuery } from "@tanstack/react-query";
import { RefreshControl, ScrollView, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

/** Activity — every send, newest first, grouped by day; refreshes itself while something is still going out. */
export default function ActivityScreen() {
  const { theme: t } = useTheme();
  const sends = useQuery({
    queryKey: queryKeys.sends,
    queryFn: listSends,
    refetchInterval: (query) => (query.state.data?.items.some((j) => isActiveStatus(j.status)) ? 4000 : false),
  });
  const inProgress = sends.data?.items.filter((j) => isActiveStatus(j.status)).length ?? 0;

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1 }}>
      <ScrollView
        contentContainerStyle={{ padding: 16, gap: 18 }}
        refreshControl={
          <RefreshControl refreshing={sends.isRefetching} onRefresh={() => void sends.refetch()} tintColor={t.color.ink} />
        }
      >
        <TopBar />
        <View style={{ marginTop: 6 }}>
          <Txt v="eyebrow">
            {sends.data
              ? [`${sends.data.totalCount} sends`, inProgress > 0 && `${inProgress} in progress`].filter(Boolean).join(" · ")
              : "Sends"}
          </Txt>
          <Txt v="h1" style={{ marginTop: 6 }}>
            Activity
          </Txt>
        </View>

        {sends.isLoading && <Spinner />}
        {sends.isError && <Txt style={{ color: t.color.danger }}>Could not load your sends. Pull down to retry.</Txt>}
        {sends.data?.items.length === 0 && (
          <Card style={{ padding: 20 }}>
            <Txt v="title">Nothing sent yet</Txt>
            <Txt style={{ color: t.color.muted, marginTop: 4 }}>Replies you send from Letters show up here.</Txt>
          </Card>
        )}

        {sends.data &&
          groupByDay(sends.data.items).map((group) => (
            <View key={group.label} style={{ gap: 10 }}>
              <View style={{ flexDirection: "row", alignItems: "baseline", justifyContent: "space-between" }}>
                <Txt v="h2">{group.label}</Txt>
                <Txt v="mono">
                  {group.jobs.length} {group.jobs.length === 1 ? "send" : "sends"}
                </Txt>
              </View>
              <Card raised style={{ overflow: "hidden" }}>
                {group.jobs.map((job, i) => (
                  <SendRow key={job.id} job={job} first={i === 0} />
                ))}
              </Card>
            </View>
          ))}
      </ScrollView>
    </SafeAreaView>
  );
}
