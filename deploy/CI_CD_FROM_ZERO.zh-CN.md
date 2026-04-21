# Aether Garden 从零 CI/CD 文档（Ubuntu 22.04）

本文档覆盖两件事：

1. `aether_garden` 仓库：手动触发部署前后端到服务器。
2. `aether-content` 仓库：内容 push 后，让服务器先 `git pull` 内容，再调用后端 reload 接口。

你问的关键问题：

- **仅仅内容仓库 push，不会自动让服务器本地仓库更新**。
- 你必须额外做一件事：
  - 要么在 CI 里 SSH 到服务器执行 `git pull`（本文采用）
  - 要么在服务器上部署 webhook receiver（更复杂）
  - 要么用 self-hosted runner 直接在服务器执行（也可行）

所以答案是：**能让服务器 pull，但要通过 CI 工作流主动执行 SSH + git pull。**

---

## 0. 目标状态

- 服务器系统：Ubuntu 22.04
- 域名：`duckran.top`
- 服务器目录：
  - 网站仓库：`/opt/aether_garden/aether_garden`
  - 内容仓库：`/opt/aether_garden/aether_garden.content`
- 后端服务名：`aether-garden-api`
- 后端监听：`127.0.0.1:5109`

---

## 1. 一次性服务器准备

以下命令在服务器执行。

```bash
sudo apt-get update
sudo apt-get install -y nginx rsync curl git
```

安装站点配置和后端服务（模板在 `aether_garden/deploy/`）：

```bash
sudo cp /opt/aether_garden/aether_garden/deploy/aether-garden-api.service /etc/systemd/system/
sudo cp /opt/aether_garden/aether_garden/deploy/nginx.duckran.top.conf /etc/nginx/sites-available/duckran.top.conf
sudo ln -sf /etc/nginx/sites-available/duckran.top.conf /etc/nginx/sites-enabled/duckran.top.conf
sudo systemctl daemon-reload
sudo nginx -t
sudo systemctl reload nginx
```

HTTPS 证书（首次）：

```bash
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d duckran.top -d www.duckran.top
```

---

## 2. 后端生产配置

目标文件：

- `/opt/aether_garden/aether_garden/publish/aether_garden-be/appsettings.Production.json`

可从模板复制：

```bash
cp /opt/aether_garden/aether_garden/deploy/appsettings.Production.json /opt/aether_garden/aether_garden/publish/aether_garden-be/appsettings.Production.json
```

必须确认：

- `Content:RootPath` = `/opt/aether_garden/aether_garden.content`
- `InternalAuth:ReloadToken` 是强随机串（后续会用于 content 仓库 secret）
- CORS 包含 `https://duckran.top` 和 `https://www.duckran.top`

---

## 3. GitHub Secrets 配置

### 3.1 `aether_garden` 仓库（网站部署）

路径：`Settings -> Environments -> production -> Secrets`

- `DEPLOY_HOST`：服务器 IP 或域名
- `DEPLOY_USER`：部署用户
- `DEPLOY_SSH_KEY`：部署用户私钥
- `DEPLOY_PORT`：可选，默认 `22`

### 3.2 `aether-content` 仓库（内容同步+重载）

路径：`Settings -> Secrets and variables -> Actions`

- `DEPLOY_HOST`：服务器 IP 或域名
- `DEPLOY_USER`：部署用户
- `DEPLOY_SSH_KEY`：部署用户私钥
- `DEPLOY_PORT`：可选，默认 `22`
- `WEBSITE_RELOAD_URL`：`https://duckran.top/internal/content/reload`
- `WEBSITE_RELOAD_TOKEN`：与后端 `InternalAuth:ReloadToken` 完全一致
- `CONTENT_REPO_DIR`：可选，默认 `/opt/aether_garden/aether_garden.content`

---

## 4. 网站仓库（aether_garden）部署流程

你已经有工作流文件：

- `.github/workflows/manual-deploy.yml`

触发方式：

1. 打开 GitHub Actions
2. 选择 `Manual Deploy`
3. 点击 `Run workflow`
4. `ref` 填 `main`（或指定 tag/commit）

它会自动完成：

- 前端 build（`VITE_API_BASE_URL=https://duckran.top/api`）
- 后端 publish（self-contained linux-x64）
- 上传产物到服务器
- 执行 `deploy/deploy.sh` 完成落地、重启后端、reload Nginx、健康检查

---

## 5. 内容仓库（aether-content）push 自动同步并重载

工作流文件放在：

- `aether-content/.github/workflows/content-sync-and-reload.yml`

逻辑：

1. 内容仓库发生 push
2. GitHub Actions SSH 到服务器内容目录执行：
   - `git fetch --all --prune`
   - `git checkout <当前提交 SHA>`
3. 然后调用：`POST /internal/content/reload`

这就是“push 后让服务器 pull（准确讲是 fetch + checkout 指定提交）”的实现。

---

## 6. 验证清单

### 6.1 网站部署验证

```bash
curl -i https://duckran.top/api/blog
```

```bash
journalctl -u aether-garden-api -n 100 --no-pager
```

### 6.2 内容 push 验证

1. 在 `aether-content` 新增或修改一篇 `content/blog/*.md`
2. push 到 `main`
3. 查看 `content-sync-and-reload` 工作流成功
4. 再次请求：

```bash
curl -i https://duckran.top/api/blog
```

若数据变化，即流程成功。

---

## 7. 常见故障

- `reload 401`：`WEBSITE_RELOAD_TOKEN` 与后端 token 不一致。
- `SSH permission denied`：私钥不对，或服务器 `authorized_keys` 未配置。
- `git checkout <sha> 失败`：服务器内容仓库 remote 配置或权限不正确。
- 内容没变化但工作流成功：检查服务器上 `CONTENT_REPO_DIR` 是否写错。

---

## 8. 推荐权限收敛

部署用户建议只开放必要 sudo 命令，不给全量 root。
如果你需要，我可以再给你一份最小 sudoers 模板。
