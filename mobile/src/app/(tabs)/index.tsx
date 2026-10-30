import type { InboxMessage } from "@/api/schemas";
import { LetterCard } from "@/components/letter-card";
import { TopBar } from "@/components/top-bar";
import { Card, Chip, Spinner, Txt } from "@/components/ui";
import { useInbox, useMailbox } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import { router } from "expo-router";
import { MailOpen, Search, X } from "lucide-react-native";
import { useState } from "react";
import { RefreshControl, ScrollView, TextInput, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

/** Letters — the phone's home: the replies waiting for you first, then everything earlier. */
export default function LettersScreen() {
  const { theme: t } = useTheme();
  const { accounts, mailbox } = useMailbox();
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [swipedId, setSwipedId] = useState<string | null>(null);
  const inbox = useInbox(mailbox?.id, appliedSearch);

  const items = inbox.data?.items ?? [];
  const unread = items.filter((m) => !m.isRead).length;
  const waiting = items.filter((m) => !m.isRead);
  const earlier = unreadOnly ? [] : items.filter((m) => m.isRead);

  const open = (m: InboxMessage) =>
    router.push({ pathname: "/message/[id]", params: { id: m.id, accountId: mailbox!.id } });
  const reply = (m: InboxMessage) =>
    router.push({ pathname: "/reply/[id]", params: { id: m.id, accountId: mailbox!.id } });

  const letter = (m: InboxMessage) => (
    <LetterCard
      key={m.id}
      message={m}
      revealed={swipedId === m.id}
      onReveal={(value) => setSwipedId(value ? m.id : null)}
      onOpen={() => open(m)}
      onReply={() => reply(m)}
    />
  );

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1 }}>
      <ScrollView
        contentContainerStyle={{ padding: 16, gap: 14 }}
        keyboardShouldPersistTaps="handled"
        refreshControl={
          <RefreshControl refreshing={inbox.isRefetching} onRefresh={() => void inbox.refetch()} tintColor={t.color.ink} />
        }
      >
        <TopBar />
        <View style={{ marginTop: 6 }}>
          <Txt v="eyebrow">
            {new Date().toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" })}
          </Txt>
          <Txt v="h1" style={{ marginTop: 6 }}>
            {!inbox.data ? "Letters" : unread > 0 ? `${unread} ${unread === 1 ? "letter" : "letters"} waiting` : "All caught up"}
          </Txt>
        </View>

        {accounts.data && !mailbox && (
          <Card style={{ padding: 18, gap: 6 }}>
            <Txt v="title">Connect a mailbox first</Txt>
            <Txt v="body" style={{ color: t.color.muted }}>
              Connect Gmail or Outlook with read access on the web app, then pull down here to refresh.
            </Txt>
          </Card>
        )}

        {mailbox && (
          <>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }}>
              <Chip label="All" on={!unreadOnly} onPress={() => setUnreadOnly(false)} />
              <Chip label={`Unread · ${unread}`} on={unreadOnly} onPress={() => setUnreadOnly(true)} />
              <Chip
                label={appliedSearch ? `“${appliedSearch}”` : "Search"}
                icon={Search}
                on={!!appliedSearch}
                onPress={() => setSearchOpen((v) => !v)}
              />
              {!!appliedSearch && (
                <Chip
                  label="Clear"
                  icon={X}
                  onPress={() => {
                    setSearch("");
                    setAppliedSearch("");
                    setSearchOpen(false);
                  }}
                />
              )}
            </ScrollView>

            {searchOpen && (
              <TextInput
                autoFocus
                value={search}
                onChangeText={setSearch}
                onSubmitEditing={() => setAppliedSearch(search.trim())}
                returnKeyType="search"
                placeholder="Search this mailbox…"
                placeholderTextColor={t.color.muted}
                accessibilityLabel="Search this mailbox"
                style={{
                  height: 44,
                  paddingHorizontal: 14,
                  borderRadius: 12,
                  borderWidth: t.borderWidth,
                  borderColor: t.color.border,
                  backgroundColor: t.color.card,
                  color: t.color.ink,
                  fontFamily: t.font.regular,
                  fontSize: 15,
                }}
              />
            )}

            {inbox.isLoading && <Spinner />}
            {inbox.isError && <Txt style={{ color: t.color.danger }}>Could not load this mailbox. Pull down to retry.</Txt>}
            {inbox.data && waiting.length + earlier.length === 0 && (
              <Card style={{ padding: 24, alignItems: "center", gap: 8 }}>
                <MailOpen size={26} color={t.color.muted} />
                <Txt v="title">{appliedSearch ? "No letters match" : unreadOnly ? "Nothing unread" : "No letters yet"}</Txt>
              </Card>
            )}

            {waiting.length > 0 && <View style={{ gap: 12 }}>{waiting.map(letter)}</View>}
            {earlier.length > 0 && (
              <View style={{ gap: 12 }}>
                {waiting.length > 0 && <Txt v="mono" style={{ marginTop: 4, textTransform: "uppercase", letterSpacing: 0.6 }}>Earlier</Txt>}
                {earlier.map(letter)}
              </View>
            )}
            {waiting.length > 0 && !swipedId && (
              <Txt v="mono" style={{ textAlign: "center", fontSize: 11 }}>
                Swipe a letter left to reply
              </Txt>
            )}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
