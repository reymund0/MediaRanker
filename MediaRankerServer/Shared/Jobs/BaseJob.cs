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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Enabled)
        {
            logger.LogInformation("{JobName} is disabled.", config.JobName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nextRunUtc = nowUtc.Date.AddHours(config.ScheduleHourUtc);
            if (nextRunUtc <= nowUtc) nextRunUtc = nextRunUtc.AddDays(1);

            var delay = nextRunUtc - nowUtc;
            logger.LogInformation(
                "Next {JobName} scheduled for {NextRun} UTC (Delay: {Delay})",
                config.JobName, nextRunUtc, delay);

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                await RunJobAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running {JobName}.", config.JobName);
            }
        }
    }
}