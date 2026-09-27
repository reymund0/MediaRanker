using System.Diagnostics;

namespace MediaRankerServer.Modules.Media.Services;

public enum ImportStopReason
{
    None,
    Completed,
    Disabled,
    Canceled,
    LeaseBusy,
    LeaseLost,
    SessionHttpLimit,
    SessionAdmissionLimit,
    SessionDeadline,
    UnitPageLimit,
    UnitHttpLimit,
    UnitAdmissionLimit,
    UnitDeadline,
    ProviderCooldown,
    AdmissionBlocked,
    ProviderFailure,
    NonIncreasingPage
}

public sealed record ImportSessionLimits(int MaxHttpAttempts, int MaxAdmissionRows, TimeSpan MaxDuration)
{
    public bool IsValid => MaxHttpAttempts > 0 && MaxAdmissionRows > 0
        && MaxDuration > TimeSpan.Zero && MaxDuration != Timeout.InfiniteTimeSpan
        && MaxDuration <= TimeSpan.FromMilliseconds(uint.MaxValue - 1);
}

public sealed record ImportWorkUnitLimits(
    int MaxPages,
    int MaxHttpAttempts,
    int MaxAdmissionRows,
    int MaxAdmissionBatches,
    TimeSpan MaxDuration)
{
    public bool IsValid => MaxPages > 0 && MaxHttpAttempts > 0 && MaxAdmissionRows > 0
        && MaxAdmissionBatches > 0 && MaxDuration > TimeSpan.Zero
        && MaxDuration != Timeout.InfiniteTimeSpan
        && MaxDuration <= TimeSpan.FromMilliseconds(uint.MaxValue - 1);
}

/// <summary>
/// Process-local cumulative allowance for one deliberately launched import session.
/// Counters are intentionally not persisted; a new process creates a new allowance.
/// </summary>
public sealed class ImportBudgetSession : IDisposable
{
    private readonly object gate = new();
    private readonly ImportSessionLimits limits;
    private readonly long startedTimestamp;
    private readonly CancellationTokenSource deadlineCancellation;
    private int httpAttempts;
    private int admissionRows;
    private int admissionBatches;
    private readonly Dictionary<string, int> httpAttemptsByOperation = new(StringComparer.Ordinal);
    private bool disposed;
    private ImportStopReason stopReason;

    public ImportBudgetSession(ImportSessionLimits limits, TimeProvider? timeProvider = null)
    {
        if (!limits.IsValid)
            throw new ArgumentOutOfRangeException(nameof(limits), "Import session limits must be positive and finite.");

        TimeProvider = timeProvider ?? TimeProvider.System;
        this.limits = limits;
        startedTimestamp = TimeProvider.GetTimestamp();
        deadlineCancellation = new CancellationTokenSource(limits.MaxDuration, TimeProvider);
    }

    public TimeProvider TimeProvider { get; }
    public ImportSessionLimits Limits => limits;
    public CancellationToken DeadlineToken => deadlineCancellation.Token;
    public CancellationToken Token => deadlineCancellation.Token;
    public int HttpAttempts { get { lock (gate) return httpAttempts; } }
    public int AdmissionRows { get { lock (gate) return admissionRows; } }
    public int AdmissionBatches { get { lock (gate) return admissionBatches; } }
    public IReadOnlyDictionary<string, int> HttpAttemptsByOperation
    {
        get { lock (gate) return new Dictionary<string, int>(httpAttemptsByOperation, StringComparer.Ordinal); }
    }
    public TimeSpan Elapsed => TimeProvider.GetElapsedTime(startedTimestamp);
    public TimeSpan Remaining => limits.MaxDuration - Elapsed > TimeSpan.Zero ? limits.MaxDuration - Elapsed : TimeSpan.Zero;
    public ImportStopReason StopReason { get { lock (gate) return stopReason; } }

    public ImportWorkUnitBudget CreateUnit(ImportWorkUnitLimits limits)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!limits.IsValid)
            throw new ArgumentOutOfRangeException(nameof(limits), "Import work-unit limits must be positive and finite.");
        return new ImportWorkUnitBudget(this, limits);
    }

    internal bool TryReserveHttp(out ImportStopReason reason) => TryReserveHttp("other", out reason);

    internal bool TryReserveHttp(string operation, out ImportStopReason reason)
    {
        lock (gate)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ImportBudgetSession));
            if (IsExpired())
            {
                reason = ImportStopReason.SessionDeadline;
                stopReason = reason;
                deadlineCancellation.Cancel();
                return false;
            }
            if (httpAttempts >= limits.MaxHttpAttempts)
            {
                reason = ImportStopReason.SessionHttpLimit;
                stopReason = reason;
                return false;
            }

            httpAttempts++;
            httpAttemptsByOperation[operation] = httpAttemptsByOperation.GetValueOrDefault(operation) + 1;
            reason = ImportStopReason.None;
            return true;
        }
    }

    internal bool TryReserveAdmission(int rows, out ImportStopReason reason)
    {
        if (rows < 0)
            throw new ArgumentOutOfRangeException(nameof(rows));

        lock (gate)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ImportBudgetSession));
            if (IsExpired())
            {
                reason = ImportStopReason.SessionDeadline;
                stopReason = reason;
                deadlineCancellation.Cancel();
                return false;
            }
            if (rows > limits.MaxAdmissionRows - admissionRows)
            {
                reason = ImportStopReason.SessionAdmissionLimit;
                stopReason = reason;
                return false;
            }

            admissionRows += rows;
            reason = ImportStopReason.None;
            return true;
        }
    }

    internal void RecordAdmissionBatch()
    {
        lock (gate)
            admissionBatches++;
    }

    internal bool IsExpired()
    {
        return TimeProvider.GetElapsedTime(startedTimestamp) >= limits.MaxDuration;
    }

    internal void SetStopReason(ImportStopReason reason)
    {
        if (reason == ImportStopReason.None)
            return;
        lock (gate)
            stopReason = reason;
    }

    public CancellationTokenSource CreateLinkedTokenSource(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DeadlineToken);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        deadlineCancellation.Dispose();
    }
}

/// <summary>Per-unit limits that also reserve from the cumulative session.</summary>
public sealed class ImportWorkUnitBudget : IDisposable
{
    private readonly object gate = new();
    private readonly ImportBudgetSession session;
    private readonly long startedTimestamp;
    private readonly CancellationTokenSource deadlineCancellation;
    private int pages;
    private int httpAttempts;
    private int admissionRows;
    private int admissionBatches;
    private ImportStopReason stopReason;

    internal ImportWorkUnitBudget(ImportBudgetSession session, ImportWorkUnitLimits limits)
    {
        this.session = session;
        Limits = limits;
        startedTimestamp = session.TimeProvider.GetTimestamp();
        deadlineCancellation = new CancellationTokenSource(limits.MaxDuration, session.TimeProvider);
    }

    public ImportWorkUnitLimits Limits { get; }
    public ImportBudgetSession Session => session;
    public CancellationToken Token => deadlineCancellation.Token;
    public int Pages { get { lock (gate) return pages; } }
    public int HttpAttempts { get { lock (gate) return httpAttempts; } }
    public int AdmissionRows { get { lock (gate) return admissionRows; } }
    public int AdmissionBatches { get { lock (gate) return admissionBatches; } }
    public TimeSpan Elapsed => session.TimeProvider.GetElapsedTime(startedTimestamp);
    public TimeSpan Remaining => Limits.MaxDuration - Elapsed > TimeSpan.Zero ? Limits.MaxDuration - Elapsed : TimeSpan.Zero;
    public ImportStopReason StopReason { get { lock (gate) return stopReason; } }
    public bool CanStartPage => !IsExpired() && Pages < Limits.MaxPages && HttpAttempts < Limits.MaxHttpAttempts;
    public int RemainingAdmissionRows => Math.Min(Limits.MaxAdmissionRows - AdmissionRows,
        session.Limits.MaxAdmissionRows - session.AdmissionRows);
    public int RemainingAdmissionBatches => Limits.MaxAdmissionBatches - AdmissionBatches;

    public void ThrowIfHttpUnavailable(string operation = "other")
    {
        if (!TryReserveHttp(operation, out var reason))
            throw new ImportBudgetExceededException(reason);
    }

    public bool TryReservePage(out ImportStopReason reason)
    {
        lock (gate)
        {
            if (IsExpired())
            {
                reason = session.IsExpired() ? ImportStopReason.SessionDeadline : ImportStopReason.UnitDeadline;
                stopReason = reason;
                session.SetStopReason(reason);
                return false;
            }
            if (pages >= Limits.MaxPages)
            {
                reason = ImportStopReason.UnitPageLimit;
                stopReason = reason;
                return false;
            }
            pages++;
            reason = ImportStopReason.None;
            return true;
        }
    }

    internal void RollbackPage()
    {
        lock (gate)
        {
            if (pages > 0)
                pages--;
        }
    }

    public bool TryReserveHttp(out ImportStopReason reason) => TryReserveHttp("other", out reason);

    public bool TryReserveHttp(string operation, out ImportStopReason reason)
    {
        lock (gate)
        {
            if (IsExpired())
            {
                reason = session.IsExpired() ? ImportStopReason.SessionDeadline : ImportStopReason.UnitDeadline;
                stopReason = reason;
                session.SetStopReason(reason);
                return false;
            }
            if (httpAttempts >= Limits.MaxHttpAttempts)
            {
                reason = ImportStopReason.UnitHttpLimit;
                stopReason = reason;
                return false;
            }
            if (!session.TryReserveHttp(operation, out reason))
            {
                stopReason = reason;
                return false;
            }

            httpAttempts++;
            return true;
        }
    }

    public bool TryReserveAdmission(int rows, out ImportStopReason reason)
    {
        if (rows < 0)
            throw new ArgumentOutOfRangeException(nameof(rows));

        lock (gate)
        {
            if (IsExpired())
            {
                reason = session.IsExpired() ? ImportStopReason.SessionDeadline : ImportStopReason.UnitDeadline;
                stopReason = reason;
                session.SetStopReason(reason);
                return false;
            }
            if (rows > Limits.MaxAdmissionRows - admissionRows)
            {
                reason = ImportStopReason.UnitAdmissionLimit;
                stopReason = reason;
                return false;
            }
            if (!session.TryReserveAdmission(rows, out reason))
            {
                stopReason = reason;
                return false;
            }

            admissionRows += rows;
            return true;
        }
    }

    public bool TryReserveAdmissionBatch(out ImportStopReason reason)
    {
        lock (gate)
        {
            if (IsExpired())
            {
                reason = session.IsExpired() ? ImportStopReason.SessionDeadline : ImportStopReason.UnitDeadline;
                stopReason = reason;
                session.SetStopReason(reason);
                return false;
            }
            if (admissionBatches >= Limits.MaxAdmissionBatches)
            {
                reason = ImportStopReason.UnitAdmissionLimit;
                stopReason = reason;
                return false;
            }

            admissionBatches++;
            session.RecordAdmissionBatch();
            reason = ImportStopReason.None;
            return true;
        }
    }

    public CancellationTokenSource CreateLinkedTokenSource(CancellationToken cancellationToken = default)
        => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.DeadlineToken, Token);

    public void SetStopReason(ImportStopReason reason)
    {
        if (reason == ImportStopReason.None)
            return;
        lock (gate)
            stopReason = reason;
        session.SetStopReason(reason);
    }

    private bool IsExpired() => session.IsExpired() || Elapsed >= Limits.MaxDuration;

    public void Dispose() => deadlineCancellation.Dispose();
}

public sealed class ImportBudgetExceededException(ImportStopReason reason)
    : Exception($"Import budget exhausted: {reason}")
{
    public ImportStopReason Reason { get; } = reason;
}
