#!/usr/bin/env bash
#
# Bring up the full local stack for sending email from your own connected inbox:
#   Docker infra (Postgres, MinIO, WireMock) + a public tunnel for images
#   + the .NET API + the Next.js frontend.
#
# Run from Git Bash:   bash scripts/dev-up.sh
# Stop everything:     bash scripts/dev-down.sh
#
# Notes:
# - The image tunnel (cloudflared) gives MinIO a public URL so images actually
#   render in real inboxes (localhost URLs only work on this machine). Its URL
#   changes each run; the app rewrites baked-in image URLs to it at render time,
#   so you never have to touch templates.
# - OAuth redirect is configured for http://localhost:3000, so the frontend must
#   run on 3000 (appsettings.Development.json). Don't change FRONTEND_PORT unless
#   you also update the Azure/Google redirect URIs and that config.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

LOGDIR="$ROOT/.dev-logs"
mkdir -p "$LOGDIR"

FRONTEND_PORT="${FRONTEND_PORT:-3000}"
BACKEND_PORT="${BACKEND_PORT:-5001}"
CLOUDFLARED="${CLOUDFLARED:-$HOME/cloudflared.exe}"
DOCKER_DESKTOP="${DOCKER_DESKTOP:-/c/Program Files/Docker/Docker/Docker Desktop.exe}"

echo "==> Ensuring Docker is running..."
if ! docker info >/dev/null 2>&1; then
  if [ -x "$DOCKER_DESKTOP" ] || [ -f "$DOCKER_DESKTOP" ]; then
    echo "    Launching Docker Desktop (this can take a minute)..."
    "$DOCKER_DESKTOP" >/dev/null 2>&1 &
  fi
  until docker info >/dev/null 2>&1; do sleep 3; done
fi

echo "==> Starting infra (postgres, minio, wiremock)..."
docker compose up -d >/dev/null

# ---- Image tunnel (optional but needed for images to load in real inboxes) ----
PUBLIC_BASE="http://localhost:9000/mth-public"
CF_BIN=""
if command -v cloudflared >/dev/null 2>&1; then CF_BIN="cloudflared"; elif [ -x "$CLOUDFLARED" ]; then CF_BIN="$CLOUDFLARED"; fi

if [ -n "$CF_BIN" ]; then
  echo "==> Starting image tunnel (cloudflared -> MinIO :9000)..."
  : > "$LOGDIR/tunnel.log"
  "$CF_BIN" tunnel --url http://localhost:9000 >>"$LOGDIR/tunnel.log" 2>&1 &
  echo "$!" > "$LOGDIR/tunnel.pid"
  URL=""
  for _ in $(seq 1 40); do
    URL="$(grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' "$LOGDIR/tunnel.log" | head -1)"
    [ -n "$URL" ] && break
    sleep 1
  done
  if [ -n "$URL" ]; then
    PUBLIC_BASE="$URL/mth-public"
    echo "    Tunnel ready: $URL"
  else
    echo "    WARN: tunnel URL not detected in time; images may not load remotely."
    echo "          (see $LOGDIR/tunnel.log)"
  fi
else
  echo "==> cloudflared not found; skipping image tunnel."
  echo "    Text/layout emails still send fine, but images won't load in real inboxes."
  echo "    Install: download cloudflared-windows-amd64.exe to \$HOME/cloudflared.exe"
fi

# ---- Backend ----
echo "==> Starting backend on :$BACKEND_PORT ..."
: > "$LOGDIR/backend.log"
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://localhost:$BACKEND_PORT" \
Storage__PublicBaseUrl="$PUBLIC_BASE" \
  dotnet run --project MailTemplateHub.Api --no-launch-profile >>"$LOGDIR/backend.log" 2>&1 &
echo "$!" > "$LOGDIR/backend.pid"

# ---- Frontend ----
echo "==> Starting frontend on :$FRONTEND_PORT ..."
: > "$LOGDIR/frontend.log"
( cd frontend && npm run dev -- -p "$FRONTEND_PORT" >>"$LOGDIR/frontend.log" 2>&1 ) &
echo "$!" > "$LOGDIR/frontend.pid"

echo "==> Waiting for services to come up..."
for _ in $(seq 1 60); do
  curl -s "http://localhost:$BACKEND_PORT/healthz" 2>/dev/null | grep -q Healthy && break
  sleep 2
done
for _ in $(seq 1 60); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$FRONTEND_PORT" 2>/dev/null)" = "200" ] && break
  sleep 2
done

echo ""
echo "  ✅ Ready"
echo "  App:     http://localhost:$FRONTEND_PORT"
echo "  API:     http://localhost:$BACKEND_PORT   (health: /healthz)"
echo "  Images:  $PUBLIC_BASE"
echo "  Logs:    $LOGDIR/{backend,frontend,tunnel}.log"
echo "  Stop:    bash scripts/dev-down.sh"
