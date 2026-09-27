namespace MediaRankerServer.Modules.Media.Providers;

/// <summary>Shared cooldown so independently-created TMDB typed clients cannot hot-loop an outage.</summary>
public sealed class TmdbRequestCooldown(TmdbOptions? options = null)
{
    private readonly object gate = new();
    private readonly SemaphoreSlim concurrency = new((options ?? new()).MaxConcurrentRequests);
    private readonly TimeSpan interval = TimeSpan.FromSeconds(1d / (options ?? new()).RequestsPerSecond);
    private DateTimeOffset blockedUntil;
    private DateTimeOffset nextRequestAt;

    public bool IsBlocked
    {
        get { lock (gate) return blockedUntil > DateTimeOffset.UtcNow; }
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        ThrowIfBlocked();
        await concurrency.WaitAsync(ct);
        try
        {
            TimeSpan delay;
            lock (gate)
            {
                var now = DateTimeOffset.UtcNow;
                var scheduled = nextRequestAt > now ? nextRequestAt : now;
                nextRequestAt = scheduled.Add(interval);
                delay = scheduled - now;
            }
            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
            ThrowIfBlocked();
            return new Lease(concurrency);
        }
        catch { concurrency.Release(); throw; }
    }

    public void ThrowIfBlocked()
    {
        TimeSpan? remaining;
        lock (gate)
            remaining = blockedUntil > DateTimeOffset.UtcNow ? blockedUntil - DateTimeOffset.UtcNow : null;
        if (remaining is not null)
            throw new ProviderRequestException("rate_limited", remaining, isLocalCooldown: true);
    }

    public void Defer(TimeSpan? retryAfter)
    {
        var duration = retryAfter is { } requested && requested > TimeSpan.Zero ? requested : TimeSpan.FromMinutes(5);
        lock (gate)
        {
            var until = DateTimeOffset.UtcNow.Add(duration);
            if (until > blockedUntil) blockedUntil = until;
        }
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? held = semaphore;
        public void Dispose() => Interlocked.Exchange(ref held, null)?.Release();
    }
}
