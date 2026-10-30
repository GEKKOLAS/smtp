import { ApiError } from "@/api/client";
import {
  getInboxMessage,
  getSend,
  getTemplate,
  isActiveStatus,
  listSends,
  listTemplates,
  queryKeys,
  replyWithTemplate,
} from "@/api/endpoints";
import type { InboxMessageDetail, SendJob } from "@/api/schemas";
import { SendRow } from "@/components/send-row";
import { TemplateThumb } from "@/components/template-thumb";
import { Button, Card, Spinner, Txt } from "@/components/ui";
import { useMailbox } from "@/hooks/data";
import { useTheme } from "@/theme/theme";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import * as Haptics from "expo-haptics";
import { router, useLocalSearchParams } from "expo-router";
import { Check, Send, X } from "lucide-react-native";
import { useEffect, useState } from "react";
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  Text,
  TextInput,
  View,
} from "react-native";
import { SafeAreaView, useSafeAreaInsets } from "react-native-safe-area-context";

const THUMB_W = 144;
const THUMB_H = 192;

const firstNameOf = (m: Pick<InboxMessageDetail, "fromName" | "fromEmail">) =>
  m.fromName?.trim().split(/\s+/)[0] || m.fromEmail;

/** Fills the obvious variables from the sender so a phone reply is usually just "pick and send". */
function suggestedValue(variable: string, m: InboxMessageDetail): string | undefined {
  const key = variable.toLowerCase().replace(/[^a-z]/g, "");
  if (["firstname", "first", "name"].includes(key)) return m.fromName ? firstNameOf(m) : undefined;
  if (["fullname", "recipientname", "contactname"].includes(key)) return m.fromName ?? undefined;
  if (["email", "recipientemail", "contactemail"].includes(key)) return m.fromEmail;
  return undefined;
}

function errorCopy(error: unknown) {
  const code = error instanceof ApiError ? error.errorCode : "";
  if (code === "send.variables_missing") return "Fill in the template variables before replying.";
  if (code === "inbox.scope_missing") return "Reconnect this account on the web to allow reading mail.";
  if (code === "send.too_large") return "The message exceeds the size limit.";
  return "Could not send the reply. Try again.";
}

/**
 * Reply with a template (design B2): pick from a carousel of real previews, check the
 * variables, send. Only the provider message id goes to the API — the backend answers
 * the original sender in the same thread. Then the "Sent" moment (B3).
 */
export default function ReplyScreen() {
  const { theme: t } = useTheme();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const { id, accountId } = useLocalSearchParams<{ id: string; accountId: string }>();
  const { accounts } = useMailbox();
  const accountEmail = accounts.data?.find((a) => a.id === accountId)?.emailAddress;

  const [templateId, setTemplateId] = useState("");
  const [subject, setSubject] = useState("");
  const [variables, setVariables] = useState<Record<string, string>>({});
  const [sent, setSent] = useState<SendJob | null>(null);

  const message = useQuery({ queryKey: queryKeys.inboxMessage(accountId, id), queryFn: () => getInboxMessage(accountId, id) });
  const templates = useQuery({ queryKey: queryKeys.templates, queryFn: listTemplates });
  const sendable = templates.data?.items.filter((x) => x.currentVersionId) ?? [];
  const selected = sendable.find((x) => x.id === templateId);
  const template = useQuery({
    queryKey: queryKeys.template(templateId),
    queryFn: () => getTemplate(templateId),
    enabled: !!templateId,
  });
  const templateVariables = template.data?.currentVersion?.variablesSchema ?? [];

  const m = message.data;
  const valueOf = (name: string) => variables[name] ?? (m && suggestedValue(name, m)) ?? "";

  const send = useMutation({
    mutationFn: () =>
      replyWithTemplate({
        accountId,
        messageId: id,
        templateVersionId: selected!.currentVersionId!,
        variables: Object.fromEntries(
          templateVariables.map((v) => [v.name, valueOf(v.name)] as const).filter(([, value]) => value !== ""),
        ),
        // Null lets the backend derive "Re: …" from the original message.
        subjectOverride: subject.trim() || null,
      }),
    onSuccess: (job) => {
      setSent(job);
      void queryClient.invalidateQueries({ queryKey: queryKeys.sends });
    },
  });

  if (sent && m) return <Sent initial={sent} recipient={firstNameOf(m)} />;

  const canSend = !!selected && !!m && !send.isPending;

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1, backgroundColor: t.color.card }}>
      <View
        style={{
          height: 52,
          flexDirection: "row",
          alignItems: "center",
          justifyContent: "space-between",
          paddingHorizontal: 16,
          borderBottomWidth: t.borderWidth,
          borderBottomColor: t.color.border,
        }}
      >
        <Pressable accessibilityRole="button" onPress={() => router.back()} hitSlop={10}>
          <Txt v="label" style={{ fontSize: 16, color: t.color.muted }}>
            Cancel
          </Txt>
        </Pressable>
        <Txt v="title" numberOfLines={1} style={{ flexShrink: 1, fontSize: 16 }}>
          {m ? `Reply to ${firstNameOf(m)}` : "Reply"}
        </Txt>
        <Pressable accessibilityRole="button" disabled={!canSend} onPress={() => send.mutate()} hitSlop={10}>
          <Txt v="label" style={{ fontSize: 16, color: t.color.link, opacity: canSend ? 1 : 0.4 }}>
            Send
          </Txt>
        </Pressable>
      </View>

      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === "ios" ? "padding" : undefined}>
        <ScrollView contentContainerStyle={{ padding: 16, gap: 20 }} keyboardShouldPersistTaps="handled">
          {message.isLoading && <Spinner />}
          {m && (
            <View
              style={{
                padding: 14,
                gap: 4,
                borderRadius: 14,
                backgroundColor: t.color.raised,
                borderWidth: t.borderWidth,
                borderColor: t.color.border,
              }}
            >
              <Txt v="eyebrow" numberOfLines={1} style={{ color: t.color.muted }}>
                {m.subject || "(no subject)"}
              </Txt>
              {!!m.text && (
                <Txt numberOfLines={2} style={{ fontSize: 14 }}>
                  “{m.text.trim().replace(/\s+/g, " ")}”
                </Txt>
              )}
              <Txt v="mono" numberOfLines={1}>
                from {m.fromName ? `${m.fromName} <${m.fromEmail}>` : m.fromEmail}
              </Txt>
            </View>
          )}

          <View style={{ gap: 10 }}>
            <View style={{ flexDirection: "row", alignItems: "baseline", justifyContent: "space-between" }}>
              <Txt v="h2">Pick a template</Txt>
              {templates.data && <Txt v="mono">{sendable.length} ready</Txt>}
            </View>
            {templates.isLoading && <Spinner />}
            {templates.isError && <Txt style={{ color: t.color.danger }}>Could not load your templates.</Txt>}
            {templates.data && sendable.length === 0 && (
              <Txt style={{ color: t.color.muted }}>No templates with content yet. Create one on the web app first.</Txt>
            )}
            {sendable.length > 0 && (
              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                snapToInterval={THUMB_W + 12}
                decelerationRate="fast"
                style={{ marginHorizontal: -16 }}
                contentContainerStyle={{ paddingHorizontal: 16, paddingVertical: 4, gap: 12 }}
              >
                {sendable.map((item) => {
                  const on = item.id === templateId;
                  return (
                    <Pressable
                      key={item.id}
                      accessibilityRole="radio"
                      accessibilityState={{ checked: on }}
                      accessibilityLabel={item.name}
                      onPress={() => {
                        setTemplateId(item.id);
                        if (Platform.OS !== "web") void Haptics.selectionAsync();
                      }}
                      style={{ width: THUMB_W + 6 }}
                    >
                      <View
                        style={{
                          padding: 3,
                          borderRadius: 16,
                          borderWidth: 3,
                          borderColor: on ? (t.name === "light" ? t.color.ink : t.color.primary) : "transparent",
                        }}
                      >
                        <View
                          style={{
                            borderRadius: 11,
                            overflow: "hidden",
                            borderWidth: t.borderWidth,
                            borderColor: t.color.border,
                          }}
                        >
                          <TemplateThumb versionId={item.currentVersionId} width={THUMB_W - 2 * t.borderWidth} height={THUMB_H} />
                        </View>
                        {on && (
                          <View
                            style={{
                              position: "absolute",
                              top: 10,
                              right: 10,
                              width: 24,
                              height: 24,
                              borderRadius: 12,
                              alignItems: "center",
                              justifyContent: "center",
                              backgroundColor: t.color.primary,
                              borderWidth: t.name === "light" ? 2 : 0,
                              borderColor: t.color.border,
                            }}
                          >
                            <Check size={13} strokeWidth={3} color={t.color.onPrimary} />
                          </View>
                        )}
                      </View>
                      <Txt v="label" numberOfLines={1} style={{ marginTop: 6, fontSize: 13, paddingHorizontal: 3 }}>
                        {item.name}
                      </Txt>
                    </Pressable>
                  );
                })}
              </ScrollView>
            )}
          </View>

          {selected && (
            <View style={{ gap: 10 }}>
              <Txt v="h2">Personalize</Txt>
              <Card style={{ overflow: "hidden" }}>
                <Field label="Subject" value={subject} onChangeText={setSubject} placeholder={m?.replySubject ?? ""} first />
                {template.isLoading && <Spinner />}
                {templateVariables.map((v) => (
                  <Field
                    key={v.name}
                    mono
                    label={`{{${v.name}}}`}
                    value={valueOf(v.name)}
                    onChangeText={(value) => setVariables((prev) => ({ ...prev, [v.name]: value }))}
                    placeholder={v.sample ?? v.default ?? (v.required ? "Required" : "Optional")}
                    keyboardType={v.type === "url" ? "url" : "default"}
                  />
                ))}
              </Card>
            </View>
          )}

          <Txt v="mono">Goes back in the same thread{accountEmail ? ` · from ${accountEmail}` : ""}</Txt>
          {send.isError && <Txt style={{ color: t.color.danger }}>{errorCopy(send.error)}</Txt>}
        </ScrollView>

        <View
          style={{
            paddingHorizontal: 16,
            paddingTop: 12,
            paddingBottom: Math.max(insets.bottom, 16),
            borderTopWidth: t.borderWidth,
            borderTopColor: t.color.border,
          }}
        >
          <Button
            size="lg"
            icon={Send}
            loading={send.isPending}
            disabled={!canSend}
            label={send.isPending ? "Sending…" : selected ? "Send reply" : "Pick a template to reply"}
            onPress={() => send.mutate()}
          />
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

function Field({
  label,
  mono,
  first,
  ...input
}: {
  label: string;
  mono?: boolean;
  first?: boolean;
  value: string;
  onChangeText: (value: string) => void;
  placeholder: string;
  keyboardType?: "default" | "url";
}) {
  const { theme: t } = useTheme();
  return (
    <View
      style={{
        flexDirection: "row",
        alignItems: "center",
        gap: 12,
        paddingHorizontal: 14,
        minHeight: 48,
        borderTopWidth: first ? 0 : t.borderWidth,
        borderTopColor: t.color.hairline,
      }}
    >
      <Text
        numberOfLines={1}
        style={{
          width: 112,
          fontFamily: mono ? t.mono.bold : t.font.semibold,
          fontSize: mono ? 13 : 14,
          color: t.color.ink,
        }}
      >
        {label}
      </Text>
      <TextInput
        {...input}
        accessibilityLabel={label}
        autoCapitalize="none"
        placeholderTextColor={t.color.muted}
        style={{
          flex: 1,
          paddingVertical: 12,
          textAlign: "right",
          fontFamily: t.font.regular,
          fontSize: 14,
          color: t.color.ink,
        }}
      />
    </View>
  );
}

/** B3: a stamp that follows the job's live status, then today's activity. */
function Sent({ initial, recipient }: { initial: SendJob; recipient: string }) {
  const { theme: t } = useTheme();
  const insets = useSafeAreaInsets();
  const live = useQuery({
    queryKey: queryKeys.send(initial.id),
    queryFn: () => getSend(initial.id),
    initialData: initial,
    refetchInterval: (query) => (isActiveStatus(query.state.data?.status ?? initial.status) ? 2000 : false),
  });
  const job = live.data;
  const recent = useQuery({ queryKey: queryKeys.sends, queryFn: listSends });
  const today = new Date().toDateString();
  const todays = [job, ...(recent.data?.items ?? []).filter((j) => j.id !== job.id)].filter(
    (j) => new Date(j.createdAt).toDateString() === today,
  );

  const failed = job.status === "failed" || job.status === "partiallyfailed";
  const done = job.status === "sent";
  const copy = done
    ? { stamp: "SENT", icon: Check, title: "Reply delivered", body: `${recipient} will see it in the same conversation.` }
    : failed
      ? { stamp: "FAILED", icon: X, title: "The reply didn't go out", body: "Check the account on the web app and try again." }
      : job.status === "cancelled"
        ? { stamp: "STOPPED", icon: X, title: "Reply cancelled", body: "Nothing was sent." }
        : { stamp: "SENDING", icon: null, title: "Sending your reply…", body: "This usually takes a few seconds." };

  // A haptic tick when the stamp lands on "sent".
  useEffect(() => {
    if (done && Platform.OS !== "web") void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
  }, [done]);

  const stampBg = failed ? (t.name === "light" ? "#FF7A45" : "rgba(248,113,113,0.15)") : t.name === "light" ? t.color.primary : "rgba(155,135,255,0.14)";
  const stampInk = failed ? (t.name === "light" ? "#111111" : t.color.danger) : t.name === "light" ? "#111111" : t.color.link;

  return (
    <SafeAreaView edges={["top"]} style={{ flex: 1, backgroundColor: t.color.card }}>
      <ScrollView contentContainerStyle={{ padding: 16, paddingTop: 40, gap: 22 }}>
        <View
          accessibilityRole="text"
          accessibilityLabel={copy.title}
          style={{
            alignSelf: "center",
            width: 132,
            height: 132,
            borderRadius: t.name === "light" ? 20 : 66,
            backgroundColor: stampBg,
            alignItems: "center",
            justifyContent: "center",
            transform: [{ rotate: t.tilt(-3) }],
          }}
        >
          {t.name === "light" && (
            <View
              style={{
                position: "absolute",
                top: 10,
                left: 10,
                right: 10,
                bottom: 10,
                borderRadius: 12,
                borderWidth: 2,
                borderStyle: "dashed",
                borderColor: "#111111",
              }}
            />
          )}
          {copy.icon ? (
            <copy.icon size={32} strokeWidth={2.75} color={stampInk} />
          ) : (
            <ActivityIndicator size="large" color={stampInk} />
          )}
          <Text style={{ marginTop: 4, fontFamily: t.mono.bold, fontSize: 17, letterSpacing: 1.4, color: stampInk }}>
            {copy.stamp}
          </Text>
        </View>
        <View style={{ alignItems: "center", gap: 8 }}>
          <Txt v="h1" style={{ fontSize: t.name === "light" ? 32 : 26, textAlign: "center" }}>
            {copy.title}
          </Txt>
          <Txt style={{ color: t.color.muted, textAlign: "center" }}>{copy.body}</Txt>
        </View>
        {todays.length > 0 && (
          <View style={{ gap: 10 }}>
            <View style={{ flexDirection: "row", alignItems: "baseline", justifyContent: "space-between" }}>
              <Txt v="h2">Today</Txt>
              <Txt v="mono">
                {todays.length} {todays.length === 1 ? "send" : "sends"}
              </Txt>
            </View>
            <Card raised style={{ overflow: "hidden" }}>
              {todays.slice(0, 5).map((j, i) => (
                <SendRow key={j.id} job={j} first={i === 0} />
              ))}
            </Card>
          </View>
        )}
      </ScrollView>
      <View style={{ padding: 16, paddingBottom: Math.max(insets.bottom, 16), borderTopWidth: t.borderWidth, borderTopColor: t.color.border }}>
        <Button kind="outline" size="lg" label="Back to letters" onPress={() => router.dismissTo("/")} />
      </View>
    </SafeAreaView>
  );
}
