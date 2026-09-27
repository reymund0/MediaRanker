namespace MediaRankerServer.Modules.Media.Providers;

/// <summary>Application-process-wide IGDB limiter shared by catalog and artwork calls.</summary>
public sealed class IgdbRequestLimiter(IgdbOptions options)
{
    private readonly SemaphoreSlim concurrency = new(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
    private readonly SemaphoreSlim scheduleGate = new(1, 1);
    private readonly object cooldownGate = new();
    private readonly TimeSpan interval = TimeSpan.FromSeconds(1d / options.RequestsPerSecond);
    private DateTimeOffset nextRequestAt = DateTimeOffset.MinValue;
    private DateTimeOffset blockedUntil;
    private string blockedCode = "rate_limited";

    public bool IsBlocked
    {
        get { lock (cooldownGate) return blockedUntil > DateTimeOffset.UtcNow; }
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        ThrowIfBlocked();
        await concurrency.WaitAsync(ct);
        try
        {
            await scheduleGate.WaitAsync(ct);
            TimeSpan delay;
            try
            {
                var now = DateTimeOffset.UtcNow;
                var scheduled = nextRequestAt > now ? nextRequestAt : now;
                nextRequestAt = scheduled.Add(interval);
                delay = scheduled - now;
            }
            finally
            {
                scheduleGate.Release();
            }

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);

            // A sibling request can have received a 429 while this request waited for normal spacing.
            ThrowIfBlocked();

            return new Lease(concurrency);
        }
        catch
        {
            concurrency.Release();
            throw;
        }
    }

    public Task DeferAsync(TimeSpan? retryAfter, string code, CancellationToken ct)
    {
        var cooldown = retryAfter is { } specified && specified > TimeSpan.Zero
            ? specified
            : TimeSpan.FromMinutes(5);
        lock (cooldownGate)
        {
            var deferredUntil = DateTimeOffset.UtcNow.Add(cooldown);
            if (deferredUntil > blockedUntil)
            {
                blockedUntil = deferredUntil;
                blockedCode = code;
            }
        }
        return Task.CompletedTask;
    }

    public void ThrowIfBlocked()
    {
        lock (cooldownGate)
        {
            if (blockedUntil > DateTimeOffset.UtcNow)
                throw new ProviderRequestException(blockedCode, blockedUntil - DateTimeOffset.UtcNow, isLocalCooldown: true);
        }
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? semaphore = semaphore;
        public void Dispose() => Interlocked.Exchange(ref semaphore, null)?.Release();
    }
}
