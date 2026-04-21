# Deployment for duckran.top (Ubuntu 22.04)

This directory contains ready-to-copy templates for production deployment.

## Files

- `aether-garden-api.service`: systemd unit for backend
- `nginx.duckran.top.conf`: Nginx site config for frontend + backend reverse proxy
- `appsettings.Production.json`: backend production config template
- `content-reload.workflow.yml`: GitHub Actions workflow to place in `aether_garden.content`

## Server layout (your target)

- Code root: `/opt/aether_garden`
- Website repo: `/opt/aether_garden/aether_garden`
- Content repo: `/opt/aether_garden/aether_garden.content`

## 1) Backend publish and run path

On server, publish backend to:

- `/opt/aether_garden/aether_garden/publish/aether_garden-be`

Suggested command:

```bash
cd /opt/aether_garden/aether_garden
dotnet publish aether_garden-be -c Release -r linux-x64 --self-contained true -o /opt/aether_garden/aether_garden/publish/aether_garden-be
```

## 2) Install backend config

Copy:

- `deploy/appsettings.Production.json` -> `/opt/aether_garden/aether_garden/publish/aether_garden-be/appsettings.Production.json`

Then edit `InternalAuth.ReloadToken` to a strong secret.

## 3) Install systemd service

Copy and enable:

```bash
sudo cp /opt/aether_garden/aether_garden/deploy/aether-garden-api.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now aether-garden-api
sudo systemctl status aether-garden-api
```

## 4) Build frontend and place static files

```bash
cd /opt/aether_garden/aether_garden/aether_garden-fe
VITE_API_BASE_URL="https://duckran.top/api" pnpm install --frozen-lockfile
VITE_API_BASE_URL="https://duckran.top/api" pnpm build
sudo mkdir -p /var/www/aether-garden
sudo rsync -av --delete dist/ /var/www/aether-garden/
```

## 5) Install Nginx config

```bash
sudo cp /opt/aether_garden/aether_garden/deploy/nginx.duckran.top.conf /etc/nginx/sites-available/duckran.top.conf
sudo ln -sf /etc/nginx/sites-available/duckran.top.conf /etc/nginx/sites-enabled/duckran.top.conf
sudo nginx -t
sudo systemctl reload nginx
```

## 6) HTTPS certificate

```bash
sudo apt-get update
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d duckran.top -d www.duckran.top
```

## 7) Configure content repo push trigger

In repo `aether_garden.content`:

1. Create workflow file `.github/workflows/content-reload.yml` from `deploy/content-reload.workflow.yml`
2. Add repository secrets:
   - `WEBSITE_RELOAD_URL`: `https://duckran.top/internal/content/reload`
   - `WEBSITE_RELOAD_TOKEN`: must match backend `InternalAuth.ReloadToken`

## 8) Verify

```bash
curl -i https://duckran.top/api/blog
curl -i -X POST https://duckran.top/internal/content/reload -H "X-Reload-Token: <your-token>"
journalctl -u aether-garden-api -n 100 --no-pager
```
