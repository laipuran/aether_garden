# Aether Garden

Duck Ran 的个人网站。本上下文覆盖后端提供的内容（Post、Note）、GitHub 概览、Profile，以及音乐（Apple Music 收藏和到网易云（Netease）的链接转换）。

## Content（内容）

**Post**:
一篇长文 Markdown 条目，通过 `/api/blog` 提供。
_Avoid_: article, blog entry, content item

**Note**:
一篇短文 Markdown 条目，通过 `/api/note` 提供，与 Post 同形。
_Avoid_: snippet, short post

**Content kind**:
Post 或 Note 的类型判别符；值为 `blog`/`note`，作为 `RelatedContent` 的字段，并与 `/api/blog`、`/api/note` 路由段和前端路由对齐。
_Avoid_: type, contentType

**Related content**:
与某篇 Post 或 Note 相关的条目列表，跨类型混合（可同时含 Post 与 Note），由 `/api/blog/{slug}/related` 与 `/api/note/{slug}/related` 提供；`RelatedContent` 形状只含 kind、slug、title、date。
_Avoid_: related posts, recommendations

**Slug**:
Post 或 Note 的 URL 安全、不区分大小写的标识符，每种内容各自唯一。
_Avoid_: id, key

**Excerpt**:
列表视图中展示的短纯文本摘要，取自 front matter 或从正文推导而来。
_Avoid_: summary, teaser

**Front matter**:
内容文件顶部的 YAML 元数据块，包含 slug、title、date、tags、excerpt 和 status。
_Avoid_: header, metadata block

**Status**:
内容文件的发布状态；只有标记为 `published` 的文件才会被提供。
_Avoid_: state, flag

**Paragraph**:
由渲染后的 Markdown 派生的纯文本块；即 PostDetail 作为其内容暴露的列表。
_Avoid_: content block, section

## Music（音乐）

**Track**:
音乐模块暴露的单首歌曲，将 Apple Music URL 与 Netease URL 配对。
_Avoid_: song, item

**Netease link**:
与 Track 的 Apple Music URL 配对的网易云（163.com）URL；Netease 搜索失败时缺失。
_Avoid_: conversion, netease url

**Converter**:
把 Apple Music 歌曲 URL 转成携带两种链接的 Track 的功能。
_Avoid_: link service, song resolver

## External data（外部数据）

**Fallback**:
当实时数据源不可达时返回的数据，并显式标记，让调用方知道它不是实时数据。
_Avoid_: mock, stub, placeholder

## Design conventions（设计约定）

**Interface**:
经 DI 注入的类只在存在真实 seam 时才拥有接口：两个或更多 adapter，或明确的测试替身需求。单一实现以具体类型被消费。参见 docs/adr/0002-interfaces-only-for-real-seams.md。
_Avoid_: interface per service, speculative abstraction
