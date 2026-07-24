#!/usr/bin/env bash
#
# Stop everything started by scripts/dev-up.sh:
#   frontend + backend (by port) + image tunnel + Docker infra.
# Docker volumes are preserved (data survives); only the containers stop.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

FRONTEND_PORT="${FRONTEND_PORT:-3000}"
BACKEND_PORT="${BACKEND_PORT:-5001}"

kill_port() {
  local port="$1"
  local pids
  pids="$(netstat -ano 2>/dev/null | grep LISTENING | grep ":$port " | awk '{print $NF}' | sort -u)"
  for pid in $pids; do
    [ -n "$pid" ] && taskkill //PID "$pid" //F >/dev/null 2>&1 && echo "    stopped pid $pid on :$port"
  done
}

echo "==> Stopping frontend (:$FRONTEND_PORT) and backend (:$BACKEND_PORT)..."
kill_port "$FRONTEND_PORT"
kill_port "$BACKEND_PORT"

echo "==> Stopping image tunnel..."
taskkill //IM cloudflared.exe //F >/dev/null 2>&1 && echo "    stopped cloudflared" || echo "    (no tunnel running)"

echo "==> Stopping Docker infra (data is kept)..."
docker compose stop >/dev/null 2>&1 && echo "    infra stopped" || echo "    (compose stop skipped)"

echo "  ✅ All stopped."
