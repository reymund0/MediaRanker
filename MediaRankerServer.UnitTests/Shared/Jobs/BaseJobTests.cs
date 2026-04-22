using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Shared.Jobs;

public class BaseJobTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotRunJob()
    {
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var options = Options.Create(new TestJobOptions
        {
            Enabled = false,
            ScheduleHourUtc = 0
        });

        var job = new TestJob(scopeFactory, options, NullLogger.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await job.ExecuteForTestAsync(cts.Token);

        Assert.Equal(0, job.RunCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnabled_LogsNextRun_AndStopsOnCancellation()
    {
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        // Pick a future hour so the job enters delay path (and we cancel quickly).
        var futureHour = (DateTimeOffset.UtcNow.Hour + 1) % 24;

        var options = Options.Create(new TestJobOptions
        {
            Enabled = true,
            ScheduleHourUtc = futureHour
        });

        var job = new TestJob(scopeFactory, options, NullLogger.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await job.ExecuteForTestAsync(cts.Token);

        Assert.Equal(0, job.RunCount); // Should not run before cancellation interrupts delay.
    }

    private sealed class TestJob : BaseJob<TestJobOptions>
    {
        public int RunCount { get; private set; }

        public TestJob(
            IServiceScopeFactory scopeFactory,
            IOptions<TestJobOptions> options,
            ILogger logger)
            : base(scopeFactory, options, logger)
        {
        }

        public Task ExecuteForTestAsync(CancellationToken ct) => ExecuteAsync(ct);

        protected override Task RunJobAsync(IServiceProvider serviceProvider, CancellationToken ct)
        {
            RunCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestJobOptions : BaseJobOptions
    {
        public override string JobName => "TestJob";
        public override bool Enabled { get; set; } = true;
        public override int ScheduleHourUtc { get; set; } = 0;
    }
}