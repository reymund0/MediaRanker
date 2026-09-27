namespace MediaRankerServer.Modules.Media.Jobs;

/// <summary>Both catalog schedules wait for the selected bootstrap's final outcome.</summary>
public sealed class CatalogScheduleGate
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string provider;

    public CatalogScheduleGate(CatalogBootstrapOptions options)
    {
        provider = options.Provider;
        if (!options.Enabled) completion.SetResult(true);
    }

    public Task<bool> WaitForSchedulesAsync(CancellationToken ct) => completion.Task.WaitAsync(ct);

    public void FinishBootstrap(string selectedProvider, bool succeeded)
    {
        if (provider != selectedProvider)
            throw new InvalidOperationException("Only the selected catalog can finish bootstrap.");
        // A failure cannot be undone by a subsequent work unit or a scheduled invocation.
        completion.TrySetResult(succeeded);
    }

    public static DateTimeOffset NextDailyRun(DateTimeOffset now, int hourUtc)
    {
        var utc = now.ToUniversalTime();
        var next = new DateTimeOffset(utc.Year, utc.Month, utc.Day, hourUtc, 0, 0, TimeSpan.Zero);
        return next <= utc ? next.AddDays(1) : next;
    }
}
