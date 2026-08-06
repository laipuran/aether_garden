# Apple Music 数据通过抓取的开发者令牌获取

`AppleMusicService`（具体抓取逻辑在 `AppleMusicDevTokenProvider`）从苹果私有 Web API（`amp-api.music.apple.com`）读取播放列表和歌曲，该 API 要求 `Bearer` 令牌。我们没有注册 MusicKit 开发者令牌，而是在运行时抓取一个：下载苹果 Web 播放器的 JavaScript 包，用正则从其中提取 JWT。

抓取不需要任何凭据或 Apple 账号，但本质上很脆弱——苹果随时可能移动包文件、改动令牌格式或封禁端点。我们接受这一点：失败时模块降级为空结果（歌曲被跳过，转换返回 not-found）而非崩溃，端点是尽力而为的。若抓取失效，回退方案是从注册的开发者密钥生成我们自己的 MusicKit 令牌。
