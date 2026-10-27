# 接口只存在于真实的 seam 处

经 DI 注入的类只有在确有东西跨越它发生变化时，才应拥有接口。单一具体实现是假设的 seam，而非真实的 seam，因此消费者直接使用具体类型。只有出现第二个 adapter，或测试确实需要替换实现时，才提炼接口。

两个用例虽各只有一个 adapter，仍保留接口：

- `IEndpointModule` 因数量豁免：六个模块实现它，`Program.cs` 通过 `GetServices<IEndpointModule>()` 发现并按特性开关加载，是真实的多态 seam。
- `IContentProvider` / `IContentReloadService` 因接口隔离豁免：把读取面（Blog/Notes 模块）与带鉴权的重载面（`InternalModule`）分开，每个消费者只看到自己可调用的成员。

2026-08-06 已应用：将 `IProfileProvider`、`IGithubOverviewService`、`INeteaseMusicService`、`IAppleMusicDevTokenProvider` 折叠进各自的具体类，并删除从未被读取的 `CorsOptions` 绑定。`IAppleMusicService` 因属深模块（两个方法的接口背后是缓存与转换子系统）而保留；若出现第二个 adapter，再行复议。
