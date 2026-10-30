import { View, type StyleProp, type ViewStyle } from "react-native";

// react-native-webview has no web build; the web preview frames the same HTML
// in a fully sandboxed iframe (no scripts, no same-origin).
export function HtmlView({
  html,
  style,
  interactive = true,
}: {
  html: string;
  style?: StyleProp<ViewStyle>;
  interactive?: boolean;
}) {
  return (
    <View style={[{ backgroundColor: "#FFFFFF", overflow: "hidden" }, style]} pointerEvents={interactive ? "auto" : "none"}>
      <iframe
        title="Email"
        sandbox=""
        srcDoc={html}
        scrolling={interactive ? "auto" : "no"}
        style={{ border: 0, width: "100%", height: "100%", display: "block" }}
      />
    </View>
  );
}
