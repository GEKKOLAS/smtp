import { Linking, type StyleProp, type ViewStyle } from "react-native";
import { WebView } from "react-native-webview";

/**
 * Renders email HTML. Scripts stay off (the markup is sender- or template-controlled)
 * and links open in the system browser instead of inside the frame.
 */
export function HtmlView({
  html,
  style,
  interactive = true,
}: {
  html: string;
  style?: StyleProp<ViewStyle>;
  /** Thumbnails pass false: no scrolling, no taps. */
  interactive?: boolean;
}) {
  return (
    <WebView
      originWhitelist={["*"]}
      source={{ html }}
      javaScriptEnabled={false}
      scrollEnabled={interactive}
      pointerEvents={interactive ? "auto" : "none"}
      showsVerticalScrollIndicator={false}
      setSupportMultipleWindows={false}
      style={[{ backgroundColor: "#FFFFFF" }, style]}
      onShouldStartLoadWithRequest={(request) => {
        if (request.url === "about:blank" || request.url.startsWith("data:")) return true;
        if (interactive && /^(https?|mailto):/.test(request.url)) void Linking.openURL(request.url);
        return false;
      }}
    />
  );
}
