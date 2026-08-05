using aether_garden_be.Services.Music;

namespace aether_garden_be.Tests;

public sealed class RefreshIfStaleAsyncTests
{
    private sealed record CacheEntry(int Value, DateTimeOffset ExpiresAt)
    {
        public static CacheEntry Fresh(int value) => new(value, DateTimeOffset.UtcNow.AddHours(1));

        public static CacheEntry Expired(int value) => new(value, DateTimeOffset.UtcNow.AddHours(-1));
    }

    [Fact]
    public async Task ReturnsCachedValue_WhenValid_WithoutCallingRefresh()
    {
        var cached = CacheEntry.Fresh(1);
        var refreshCalls = 0;

        var result = await AppleMusicService.RefreshIfStaleAsync(
            () => cached,
            entry => DateTimeOffset.UtcNow < entry.ExpiresAt,
            _ =>
            {
                refreshCalls++;
                return Task.FromResult<CacheEntry?>(CacheEntry.Fresh(2));
            },
            new SemaphoreSlim(1, 1),
            CancellationToken.None,
            _ => { }
        );

        Assert.Equal(1, result?.Value);
        Assert.Equal(0, refreshCalls);
    }

    [Fact]
    public async Task Refreshes_WhenExpired_AndSetsNewValue()
    {
        var cached = CacheEntry.Expired(1);
        var setValue = -1;

        var result = await AppleMusicService.RefreshIfStaleAsync(
            () => cached,
            entry => DateTimeOffset.UtcNow < entry.ExpiresAt,
            _ => Task.FromResult<CacheEntry?>(CacheEntry.Fresh(2)),
            new SemaphoreSlim(1, 1),
            CancellationToken.None,
            fresh => setValue = fresh.Value
        );

        Assert.Equal(2, result?.Value);
        Assert.Equal(2, setValue);
    }

    [Fact]
    public async Task ReturnsStaleValue_WhenRefreshReturnsNull_WithoutSetting()
    {
        var cached = CacheEntry.Expired(1);
        var setCalls = 0;

        var result = await AppleMusicService.RefreshIfStaleAsync(
            () => cached,
            entry => DateTimeOffset.UtcNow < entry.ExpiresAt,
            _ => Task.FromResult<CacheEntry?>(null),
            new SemaphoreSlim(1, 1),
            CancellationToken.None,
            _ => setCalls++
        );

        Assert.Equal(1, result?.Value);
        Assert.Equal(0, setCalls);
    }

    [Fact]
    public async Task ReturnsNull_WhenNoCachedValue_AndRefreshFails()
    {
        var result = await AppleMusicService.RefreshIfStaleAsync(
            () => null,
            entry => DateTimeOffset.UtcNow < entry.ExpiresAt,
            _ => Task.FromResult<CacheEntry?>(null),
            new SemaphoreSlim(1, 1),
            CancellationToken.None,
            _ => { }
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ConcurrentCallers_RefreshOnlyOnce()
    {
        var gate = new SemaphoreSlim(1, 1);
        var cached = CacheEntry.Expired(1);
        var refreshCalls = 0;
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<CacheEntry?> Call()
        {
            return AppleMusicService.RefreshIfStaleAsync(
                () => cached,
                entry => DateTimeOffset.UtcNow < entry.ExpiresAt,
                async _ =>
                {
                    Interlocked.Increment(ref refreshCalls);
                    await releaseRefresh.Task;
                    return CacheEntry.Fresh(2);
                },
                gate,
                CancellationToken.None,
                fresh => cached = fresh
            );
        }

        var first = Call();
        await Task.Delay(100);
        var second = Call();

        releaseRefresh.SetResult();

        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, refreshCalls);
        Assert.All(results, r => Assert.Equal(2, r?.Value));
    }
}
