using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.Modules.Media.Data;

/// <summary>Invocation-local counters and allowance. It is deliberately not persisted.</summary>
public sealed class ImdbImportExecution(ImdbImportOptions options)
{
    private readonly object gate = new();
    private int httpAttempts;

    public int HttpAttempts
    {
        get { lock (gate) return httpAttempts; }
    }

    public void ReserveHttpAttempt()
    {
        lock (gate)
        {
            if (httpAttempts >= options.MaxHttpAttempts)
                throw new ImdbBudgetExceededException("IMDb HTTP attempt allowance exhausted.");
            httpAttempts++;
        }
    }

    public ImdbImportCounters Counters { get; } = new();
}

public sealed class ImdbImportCounters
{
    public long RowsRead { get; private set; }
    public long ParsedRows { get; private set; }
    public long FilteredRows { get; private set; }
    public long BatchesCommitted { get; private set; }
    public long RowsInserted { get; private set; }
    public long RowsSkipped { get; private set; }
    public long CleanupRowsAffected { get; private set; }
    public long LoadRowsAffected { get; private set; }

    internal void AddRowsRead(long rows) => RowsRead += rows;
    internal void AddParsed() => ParsedRows++;
    internal void AddFiltered() => FilteredRows++;
    internal void AddBatch(ImdbImportResult result)
    {
        RowsInserted += result.Inserted;
        RowsSkipped += result.Skipped;
    }
    internal void AddBatchUnit() => BatchesCommitted++;
    internal void AddCleanup(int rows) => CleanupRowsAffected += rows;
    internal void AddLoad(int rows) => LoadRowsAffected += rows;
}

public sealed class ImdbBudgetExceededException(string message) : InvalidOperationException(message);

public sealed record ImdbFeedResult(
    long RowsRead,
    long ParsedRows,
    long FilteredRows,
    long Batches,
    long CompressedBytes,
    long DecompressedBytes);
