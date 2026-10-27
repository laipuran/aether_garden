# aether_garden Backend

ASP.NET Core Web API for profile, blog, notes, and GitHub overview.

## Content Source

Blog and notes are loaded from Markdown files in an external content repository.

Default paths (from `appsettings.json`):

- `Content:RootPath = ../aether-content`
- `Content:BlogSubPath = content/blog`
- `Content:NotesSubPath = content/notes`

Each markdown file uses YAML front matter:

```md
---
slug: building-small-tools-with-dotnet
title: 用 .NET 做小工具：从想法到可运行
excerpt: 把校园里遇到的小痛点拆成可落地需求，再用最短路径交付。
date: 2026-04-20
tags:
  - dotnet
  - tooling
status: published
updatedAt: 2026-04-20
---
正文...
```

## Run

```bash
dotnet run
```

Default backend URL: `http://localhost:5109`

## CORS

Update `appsettings.json` if your frontend is hosted on another domain:

```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:5173"
  ]
}
```

## API Endpoints

- `GET /api/profile`
- `GET /api/blog`
- `GET /api/blog/{slug}`
- `GET /api/notes`
- `GET /api/notes/{slug}`
- `GET /api/github/overview`
- `GET /api/music/favorites`
- `GET /api/music/convert?url=<URL-encoded Apple Music song URL>`
- `POST /internal/content/reload` (header: `X-Reload-Token`)

## Apple Music

Configure a playlist URL and cache window in `appsettings.json`:

```json
"AppleMusic": {
  "PlaylistUrl": "https://music.apple.com/cn/playlist/favorite-songs/pl.u-GPUoM8yJW1",
  "CacheHours": 12
}
```

The backend resolves Netease song links via the public web search endpoint and caches results.

The Apple developer token is discovered from the current Apple Music page's entry
bundle, so rotating asset filenames do not require a code change.

## Music regression tests

```bash
dotnet test ../aether_garden-be.Tests
```

Live conversion smoke test (run with the backend listening on port 5109):

```bash
curl --fail-with-body --get 'http://localhost:5109/api/music/convert' \
  --data-urlencode 'url=https://music.apple.com/cn/album/flo-lo/1731112820?i=1731112829&ls'
```
