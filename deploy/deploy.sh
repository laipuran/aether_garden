#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <release_dir>"
  exit 1
fi

RELEASE_DIR="$1"
BACKEND_TARGET="/opt/aether_garden/aether_garden/publish/aether_garden-be"
FRONTEND_TARGET="/var/www/aether-garden"
SERVICE_NAME="aether-garden-api"
HEALTH_URL="http://127.0.0.1:5109/api/blog"
HEALTH_RETRIES=30
HEALTH_SLEEP_SECONDS=2

BACKEND_TAR="$RELEASE_DIR/backend-publish.tar.gz"
FRONTEND_TAR="$RELEASE_DIR/frontend-dist.tar.gz"

if [[ ! -f "$BACKEND_TAR" ]]; then
  echo "Missing backend artifact: $BACKEND_TAR"
  exit 1
fi

if [[ ! -f "$FRONTEND_TAR" ]]; then
  echo "Missing frontend artifact: $FRONTEND_TAR"
  exit 1
fi

STAGING_DIR="$(mktemp -d /tmp/aether_deploy.XXXXXX)"
cleanup() {
  rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

mkdir -p "$STAGING_DIR/backend" "$STAGING_DIR/frontend"
tar -xzf "$BACKEND_TAR" -C "$STAGING_DIR/backend"
tar -xzf "$FRONTEND_TAR" -C "$STAGING_DIR/frontend"

if [[ -f "$BACKEND_TARGET/appsettings.Production.json" ]]; then
  cp "$BACKEND_TARGET/appsettings.Production.json" "$STAGING_DIR/backend/appsettings.Production.json"
fi

sudo mkdir -p "$BACKEND_TARGET"
sudo mkdir -p "$FRONTEND_TARGET"

sudo rsync -a --delete "$STAGING_DIR/backend/" "$BACKEND_TARGET/"
sudo rsync -a --delete "$STAGING_DIR/frontend/" "$FRONTEND_TARGET/"

sudo systemctl restart "$SERVICE_NAME"
sudo nginx -t
sudo systemctl reload nginx

healthy=false
for ((i = 1; i <= HEALTH_RETRIES; i++)); do
  if curl -fsS "$HEALTH_URL" >/dev/null; then
    healthy=true
    break
  fi

  sleep "$HEALTH_SLEEP_SECONDS"
done

if [[ "$healthy" != "true" ]]; then
  echo "Health check failed after $HEALTH_RETRIES attempts: $HEALTH_URL"
  sudo systemctl status "$SERVICE_NAME" --no-pager -l || true
  sudo journalctl -u "$SERVICE_NAME" -n 120 --no-pager || true
  exit 1
fi

echo "Deployment completed successfully."
