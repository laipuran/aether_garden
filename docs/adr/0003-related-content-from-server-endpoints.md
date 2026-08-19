# 相关内容由服务端统一端点提供

详情页右侧栏需要展示"相关文章"。`Related` 的语义是**跨类型混合**：一篇博客的相关列表可以同时含 Post 与 Note，紧扣 "related" 的字面意思。相关内容的**选择策略归服务端所有**，前端只渲染结果。

- 统一在一个后端方法上：`IContentProvider.GetRelatedContent(ContentKind kind, string slug, int limit)`。URL 上仍是两条薄路由——`GET /api/blog/{slug}/related` 与 `GET /api/note/{slug}/related`——各自硬编码 kind 字面量，不引入新的 `/api/content` 命名空间。
- 返回**精简形状** `RelatedContent { kind, slug, title, date }`，而不是全量 `PostSummary`：右栏只渲染标题、日期与链接，excerpt 和 tags 不展示；`kind` 不塞进 `PostSummary`/`PostDetail`，避免污染已有的列表与详情响应。
- `kind` 用 `ContentKind` 枚举（`Blog`/`Note`），经 `JsonStringEnumConverter` camelCase 序列化为 `"blog"`/`"note"`。选枚举是为了契约干净而非性能：openapi 生成后，前端 `kind` 类型收窄为 `"blog" | "note"` 联合，路由判别得到编译期检查。枚举成员 == 序列化值 == 路由段 == 前端路由，完全对齐。
- 选择策略：排除自身（按 `(kind, slug)` 对），按 tag 重叠数降序、并列按日期降序，**仅保留共享 ≥1 个 tag 的项**（允许空结果，前端隐藏整块），条数由配置 `Content:RelatedLimit` 控制（默认 4，方法经参数注入）。目标不存在时返回 `null` → 404；无相关项时返回 200 + 空数组。同一 slug 可在 Post 与 Note 中各存在一份（slugs 按类型各自唯一），由 `(kind, slug)` 对消歧。
- 契约走既有 openapi 管道：`make gen-api` 从后端导出 `openapi.json`，`openapi-typescript` 再生成前端 `schema.ts`。

顺带统一了 API 词汇为**单数**：`/api/notes` 更名 `/api/note`（与 `/api/blog`、`/api/music`、`/api/profile` 等既有单数特性域一致），前端路由 `/notes` 更名 `/note`，**不加重定向**、直接改干净。kind 值因此与路径段和路由零映射对齐。

未采纳的候选：客户端从现有列表端点过滤派生（选择策略暴露在前端，且无法读到 front matter 中的显式关联）；为将来的搜索 API 提前抽取 tag 匹配 helper（related 用的是重叠计分、搜索大概率是布尔筛选，共享点只有几行集合交集，属投机抽象）。将来若真需要显式 front matter 关联、搜索 tag 筛选或 `?limit=`，再基于统一的目录模型扩展。
