# Mail Template Hub — mobile

The phone client, built with Expo (SDK 58) and Expo Router. It follows design
direction B, "Inbox first":

- **Letters**: replies waiting for you, swipe left to reply.
- **Reply**: pick a template from a carousel of real previews, check the
  variables, send in the same thread. Then a live "Sent" stamp.
- **Activity**: every send grouped by day, refreshing while something is going out.
- **Templates**: browse and preview. Designing templates stays on the web app.

Light theme is Mailroom and dark is Nocturne, the same as the web app. It
follows the system setting until you pick one with the ☀/☾ button.

## Run it on your phone

1. Start the backend and the web app as usual (`dotnet run --project MailTemplateHub.Api`
   and `npm run dev` in `frontend/`). The phone talks to the Next.js dev server on
   port 3000, which proxies `/api` to the backend.
2. Install **Expo Go** on the phone. The phone and the computer must be on the same Wi-Fi.
3. Start Metro:

   ```bash
   cd mobile
   npm start
   ```

4. Scan the QR code with the camera (iOS) or Expo Go (Android) and sign in with your
   web-app account.

The API address defaults to `http://<this computer's LAN IP>:3000/api/v1`; the sign-in
screen shows the one it uses. To point somewhere else, set it in `mobile/.env.local`:

```bash
EXPO_PUBLIC_API_URL=https://your-server.example/api/v1
```

If the phone can't connect, allow Node.js through the Windows firewall on private networks.

## How sign-in works

The session is the same HttpOnly cookie the web app uses, kept by the OS cookie store.
The backend's double-submit CSRF check needs the token echoed in a header, and a native
app can't read cookies, so `POST /auth/login` also returns `csrfToken` in its body. The
app keeps it in the Keychain/Keystore (`expo-secure-store`).

## Checks

```bash
npm run typecheck
npm run lint
npx expo-doctor
```

`npm run web` opens a browser preview. Email HTML is rendered in a sandboxed iframe there,
and with `react-native-webview` on devices.
