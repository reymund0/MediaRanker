using Microsoft.Extensions.Options;

// Base class for background jobs that run on a schedule using BaseJobOptions.
public abstract class BaseJob<TOptions>(
    IServiceScopeFactory scopeFactory,
    IOptions<TOptions> options,
    ILogger logger) : BackgroundService
    where TOptions : BaseJobOptions
{
    protected TOptions config { get; } = options.Value;

    // Main method that each job must implement.
    protected abstract Task RunJobAsync(IServiceProvider serviceProvider, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!config.Enabled)
        {
            logger.LogInformation("{JobName} is disabled.", config.JobName);
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nextRunUtc = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, config.ScheduleHourUtc, 0, 0, TimeSpan.Zero);
            if (nextRunUtc <= nowUtc) nextRunUtc = nextRunUtc.AddDays(1);

            var delay = nextRunUtc - nowUtc;
            logger.LogInformation(
                "Next {JobName} scheduled for {NextRun} UTC (Delay: {Delay})",
                config.JobName, nextRunUtc, delay);

            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);
                
                using var scope = scopeFactory.CreateScope();
                await RunJobAsync(scope.ServiceProvider, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Job was cancelled, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running {JobName}.", config.JobName);
            }
        }
    }
}