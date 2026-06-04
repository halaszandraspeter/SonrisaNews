#!/usr/bin/env bash
# scripts/dev.sh — Sonrisa News
# One-command start. Native processes only. No Docker.
# Usage: ./scripts/dev.sh [--reset]

set -e

cyan() { printf "\033[36m%s\033[0m\n" "$1"; }
green() { printf "\033[32m%s\033[0m\n" "$1"; }
yellow() { printf "\033[33m%s\033[0m\n" "$1"; }
red() { printf "\033[31m%s\033[0m\n" "$1"; }

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

RESET=0
[ "$1" = "--reset" ] && RESET=1

cyan "==> Checking prerequisites"

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    red "✗ $1 not found. Install it and re-run."
    red "  See docs/roadmap/2-stack.md §9.2 for installation links."
    exit 1
  else
    green "✓ $1"
  fi
}

need dotnet
need node
need pnpm
need python3
need uv
need mailhog

# Configure Git hooks path
cyan "==> Configuring Git hooks"
git config core.hooksPath .githooks
chmod +x .githooks/pre-commit
green "✓ Git hooks installed"

# MailHog
cyan "==> Starting MailHog"
if pgrep -f MailHog >/dev/null 2>&1; then
  yellow "  MailHog already running"
else
  mailhog > /tmp/mailhog.log 2>&1 &
  sleep 1
  green "✓ MailHog started (SMTP :1025, UI :8025)"
fi

# yfinance sidecar
cyan "==> Starting yfinance sidecar"
if pgrep -f "uvicorn yfinance_service" >/dev/null 2>&1; then
  yellow "  yfinance sidecar already running"
else
  cd services/yfinance
  uv run uvicorn yfinance_service.main:create_app --factory --port 8001 > /tmp/yfinance.log 2>&1 &
  sleep 2
  cd "$repo_root"
  green "✓ yfinance sidecar started (:8001)"
fi

# .NET AppHost (boots Api + Worker with service discovery)
cyan "==> Running EF migrations"
mkdir -p data

if [ "$RESET" = "1" ]; then
  yellow "  --reset: deleting data/sonrisa.db before applying migrations"
  rm -f data/sonrisa.db
fi

dotnet ef database update --project backend/src/SonrisaNews.Infrastructure 2>&1 | tail -5
green "✓ DB migrated"

# Start the AppHost
cyan "==> Starting .NET AppHost (Api + Worker)"
if pgrep -f "SonrisaNews.AppHost" >/dev/null 2>&1; then
  yellow "  AppHost already running"
else
  dotnet run --project backend/SonrisaNews.AppHost > /tmp/apphost.log 2>&1 &
  sleep 8
  green "✓ AppHost started (Aspire dashboard :15000, Api :5080)"
fi

# Frontend
cyan "==> Starting Next.js dev server"
if pgrep -f "next dev" >/dev/null 2>&1; then
  yellow "  Next.js dev server already running"
else
  cd web
  pnpm dev > /tmp/nextdev.log 2>&1 &
  sleep 5
  cd "$repo_root"
  green "✓ Next.js dev server started (:3000)"
fi

cyan ""
cyan "==> Sonrisa News dev environment is up"
echo ""
echo "  App:           http://localhost:3000"
echo "  API:           http://localhost:5080"
echo "  Scalar (API):  http://localhost:5080/scalar/v1"
echo "  Aspire dash:   http://localhost:15000"
echo "  MailHog:       http://localhost:8025"
echo "  yfinance:      http://localhost:8001/docs"
echo ""
echo "  Tail logs:     tail -f /tmp/apphost.log /tmp/mailhog.log /tmp/yfinance.log /tmp/nextdev.log"
echo "  Stop:          pkill -f SonrisaNews.AppHost; pkill -f 'next dev'; pkill -f MailHog; pkill -f uvicorn"
echo ""
green "Happy alerting!"
