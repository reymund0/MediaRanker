using FluentAssertions;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.UnitTests.Modules.Media.Providers;

public sealed class ImportBudgetSessionTests
{
    [Fact]
    public void HttpAllowanceIsCumulativeAcrossWorkUnits()
    {
        using var session = new ImportBudgetSession(new ImportSessionLimits(2, 100, TimeSpan.FromMinutes(1)));
        var first = session.CreateUnit(new ImportWorkUnitLimits(5, 5, 100, 5, TimeSpan.FromMinutes(1)));
        var second = session.CreateUnit(new ImportWorkUnitLimits(5, 5, 100, 5, TimeSpan.FromMinutes(1)));

        first.TryReserveHttp("games", out var firstReason).Should().BeTrue();
        second.TryReserveHttp("games", out var secondReason).Should().BeTrue();
        second.TryReserveHttp("games", out var stopReason).Should().BeFalse();

        firstReason.Should().Be(ImportStopReason.None);
        secondReason.Should().Be(ImportStopReason.None);
        stopReason.Should().Be(ImportStopReason.SessionHttpLimit);
        session.HttpAttempts.Should().Be(2);
        session.HttpAttemptsByOperation.Should().ContainKey("games").WhoseValue.Should().Be(2);
    }

    [Fact]
    public void UnitAllowanceStopsWithoutResettingSession()
    {
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 100, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(1, 10, 100, 5, TimeSpan.FromMinutes(1)));

        unit.TryReservePage(out _).Should().BeTrue();
        unit.TryReservePage(out var reason).Should().BeFalse();

        reason.Should().Be(ImportStopReason.UnitPageLimit);
        unit.Pages.Should().Be(1);
        session.HttpAttempts.Should().Be(0);
    }

    [Fact]
    public void AdmissionRowsAndBatchesAreIndependentCaps()
    {
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 3, TimeSpan.FromMinutes(1)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 10, 2, 1, TimeSpan.FromMinutes(1)));

        unit.TryReserveAdmission(2, out var firstReason).Should().BeTrue();
        unit.TryReserveAdmissionBatch(out var batchReason).Should().BeTrue();
        unit.TryReserveAdmission(1, out var rowReason).Should().BeFalse();

        firstReason.Should().Be(ImportStopReason.None);
        batchReason.Should().Be(ImportStopReason.None);
        rowReason.Should().Be(ImportStopReason.UnitAdmissionLimit);
        session.AdmissionRows.Should().Be(2);
        session.AdmissionBatches.Should().Be(1);
    }

    [Fact]
    public void AdmissionAllowanceIsCumulativeAcrossUnits()
    {
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 3, TimeSpan.FromMinutes(1)));
        var first = session.CreateUnit(new ImportWorkUnitLimits(5, 10, 3, 3, TimeSpan.FromMinutes(1)));
        var second = session.CreateUnit(new ImportWorkUnitLimits(5, 10, 3, 3, TimeSpan.FromMinutes(1)));

        first.TryReserveAdmission(2, out _).Should().BeTrue();
        second.TryReserveAdmission(1, out var reason).Should().BeTrue();
        second.TryReserveAdmission(1, out var stop).Should().BeFalse();

        reason.Should().Be(ImportStopReason.None);
        stop.Should().Be(ImportStopReason.SessionAdmissionLimit);
        session.AdmissionRows.Should().Be(3);
    }

    [Fact]
    public async Task SessionDeadlineCancelsLinkedOperations()
    {
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 10, TimeSpan.FromMilliseconds(25)));
        var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 10, 10, 5, TimeSpan.FromSeconds(1)));
        using var linked = unit.CreateLinkedTokenSource();

        await Task.Delay(100);

        linked.IsCancellationRequested.Should().BeTrue();
        unit.TryReserveHttp(out var reason).Should().BeFalse();
        reason.Should().Be(ImportStopReason.SessionDeadline);
    }
}
