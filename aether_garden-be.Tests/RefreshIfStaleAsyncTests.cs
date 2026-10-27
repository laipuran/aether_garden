using aether_garden_be.Services;
using aether_garden_be.Services.Music;

namespace aether_garden_be.Tests;

public sealed class RefreshIfStaleAsyncTests
{
    private sealed record CacheEntry(int Value, DateTimeOffset ExpiresAt)
    {
        public static CacheEntry Fresh(int value) => new(value, DateTimeOffset.UtcNow.AddHours(1));

        public static CacheEntry Expired(int value) => new(value, DateTimeOffset.UtcNow.AddHours(-1));
    }

    private CacheEntry? _cache;
    private readonly RefreshGate<CacheEntry> _gate = new();
    private int _refreshCalls;

    private Task<CacheEntry?> Call(
        Func<CancellationToken, Task<CacheEntry?>> refresh,
        Action<CacheEntry> set
    )
    {
        return _gate.RefreshAsync(
            () => _cache,
            entry => DateTimeOffset.UtcNow < entry.ExpiresAt,
            refresh,
            CancellationToken.None,
            set
        );
    }

    [Fact]
    public async Task ReturnsCachedValue_WhenValid_WithoutCallingRefresh()
    {
        _cache = CacheEntry.Fresh(1);

        var result = await Call(
            _ =>
            {
                _refreshCalls++;
                return Task.FromResult<CacheEntry?>(CacheEntry.Fresh(2));
            },
            fresh => _cache = fresh
        );

        Assert.Equal(1, result?.Value);
        Assert.Equal(0, _refreshCalls);
    }

    [Fact]
    public async Task Refreshes_WhenExpired_AndSetsNewValue()
    {
        _cache = CacheEntry.Expired(1);
        var setValue = -1;

        var result = await Call(
            _ => Task.FromResult<CacheEntry?>(CacheEntry.Fresh(2)),
            fresh =>
            {
                setValue = fresh.Value;
                _cache = fresh;
            }
        );

        Assert.Equal(2, result?.Value);
        Assert.Equal(2, setValue);
    }

    [Fact]
    public async Task ReturnsStaleValue_WhenRefreshReturnsNull_WithoutSetting()
    {
        _cache = CacheEntry.Expired(1);
        var setCalls = 0;

        var result = await Call(_ => Task.FromResult<CacheEntry?>(null), _ => setCalls++);

        Assert.Equal(1, result?.Value);
        Assert.Equal(0, setCalls);
    }

    [Fact]
    public async Task ReturnsNull_WhenNoCachedValue_AndRefreshFails()
    {
        var result = await Call(_ => Task.FromResult<CacheEntry?>(null), _ => { });

        Assert.Null(result);
    }

    [Fact]
    public async Task ConcurrentCallers_RefreshOnlyOnce()
    {
        _cache = CacheEntry.Expired(1);
        var releaseRefresh = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        Task<CacheEntry?> CallConcurrently()
        {
            return Call(
                async _ =>
                {
                    Interlocked.Increment(ref _refreshCalls);
                    await releaseRefresh.Task;
                    return CacheEntry.Fresh(2);
                },
                fresh => _cache = fresh
            );
        }

        var first = CallConcurrently();
        await Task.Delay(100);
        var second = CallConcurrently();

        releaseRefresh.SetResult();

        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, _refreshCalls);
        Assert.All(results, r => Assert.Equal(2, r?.Value));
    }

    [Fact]
    public async Task FailingRefresh_IsRetried_OnNextCall()
    {
        _cache = CacheEntry.Expired(1);
        _refreshCalls = 0;

        var failed = await Call(
            _ =>
            {
                _refreshCalls++;
                return Task.FromResult<CacheEntry?>(null);
            },
            _ => { }
        );

        _cache = CacheEntry.Expired(1);
        var retried = await Call(
            _ =>
            {
                _refreshCalls++;
                return Task.FromResult<CacheEntry?>(CacheEntry.Fresh(2));
            },
            fresh => _cache = fresh
        );

        Assert.Equal(1, failed?.Value);
        Assert.Equal(2, retried?.Value);
        Assert.Equal(2, _refreshCalls);
    }
}
