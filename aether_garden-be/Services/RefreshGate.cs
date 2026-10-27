namespace aether_garden_be.Services;

internal sealed class RefreshGate<T>
    where T : class
{
    private Task<T?>? _inFlight;

    internal async Task<T?> RefreshAsync(
        Func<T?> read,
        Func<T, bool> isValid,
        Func<CancellationToken, Task<T?>> refresh,
        CancellationToken ct,
        Action<T> set
    )
    {
        var cached = read();
        if (cached is not null && isValid(cached))
        {
            return cached;
        }

        var task = _inFlight;
        if (task is null)
        {
            var completion = new TaskCompletionSource<T?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var previous = Interlocked.CompareExchange(ref _inFlight, completion.Task, null);
            if (previous is null)
            {
                try
                {
                    var fresh = await refresh(ct);
                    if (fresh is not null)
                    {
                        set(fresh);
                    }

                    completion.SetResult(fresh ?? read());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }

            task = previous ?? completion.Task;
        }

        try
        {
            return await task;
        }
        finally
        {
            _ = Interlocked.CompareExchange(ref _inFlight, null, task);
        }
    }
}
