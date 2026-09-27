using FluentAssertions;
using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class CatalogScheduleGateTests
{
    [Fact]
    public async Task DefaultSchedulingDoesNotWaitForBootstrap()
    {
        var gate = new CatalogScheduleGate(new());
        (await gate.WaitForSchedulesAsync(CancellationToken.None)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothCatalogsWaitForTheSameFinalOutcome(bool succeeded)
    {
        var gate = new CatalogScheduleGate(new() { Provider = "igdb" });
        var igdb = gate.WaitForSchedulesAsync(CancellationToken.None);
        var imdb = gate.WaitForSchedulesAsync(CancellationToken.None);
        igdb.IsCompleted.Should().BeFalse();
        imdb.IsCompleted.Should().BeFalse();
        gate.FinishBootstrap("igdb", succeeded);
        (await igdb).Should().Be(succeeded);
        (await imdb).Should().Be(succeeded);
    }

    [Fact]
    public async Task AStoppedSessionCannotReenableEitherSchedule()
    {
        var gate = new CatalogScheduleGate(new() { Provider = "imdb" });
        gate.FinishBootstrap("imdb", false);
        gate.FinishBootstrap("imdb", true);
        (await gate.WaitForSchedulesAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(2, 3, 21)]
    [InlineData(3, 3, 22)]
    [InlineData(4, 3, 22)]
    public void SuccessfulCompletionUsesNextDailyTimeWithoutCatchup(int currentHour, int scheduledHour, int expectedDay)
    {
        CatalogScheduleGate.NextDailyRun(new DateTimeOffset(2026, 9, 21, currentHour, 0, 0, TimeSpan.Zero), scheduledHour)
            .Should().Be(new DateTimeOffset(2026, 9, expectedDay, scheduledHour, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task HostCancellationInterruptsWaitingCatalog()
    {
        var gate = new CatalogScheduleGate(new() { Provider = "igdb" });
        using var cancellation = new CancellationTokenSource();
        var wait = gate.WaitForSchedulesAsync(cancellation.Token);
        cancellation.Cancel();
        await FluentActions.Awaiting(() => wait).Should().ThrowAsync<OperationCanceledException>();
    }
}
