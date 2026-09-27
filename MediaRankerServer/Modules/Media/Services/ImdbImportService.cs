using System.Globalization;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public record ImdbImportRunResult(
    ImdbImportResult Basics,
    ImdbImportResult? Episodes,
    ImdbImportResult Ratings,
    bool RatingsSucceeded,
    bool Completed = false,
    bool FeedBlocked = false,
    string? StopReason = null,
    ImdbImportCounters? Counters = null,
    DateTimeOffset? RatingsCutoffUtc = null);

/// <summary>Owns one complete replay while database providers are resolved per callback unit.</summary>
public class ImdbImportService
{
    private static readonly string[] BasicsHeaders = [
        "tconst", "titleType", "primaryTitle", "originalTitle", "isAdult", "startYear", "endYear", "runtimeMinutes", "genres"];
    private static readonly string[] EpisodeHeaders = ["tconst", "parentTconst", "seasonNumber", "episodeNumber"];
    private static readonly string[] RatingsHeaders = ["tconst", "averageRating", "numVotes"];

    private readonly ImdbTsvProvider parser;
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly IImdbImportProvider? providerOverride;
    private readonly ImdbImportOptions config;
    private readonly ILogger<ImdbImportService> logger;

    [ActivatorUtilitiesConstructor]
    public ImdbImportService(
        ImdbTsvProvider parser,
        IServiceScopeFactory scopeFactory,
        IOptions<ImdbImportOptions> options,
        ILogger<ImdbImportService> logger)
    {
        this.parser = parser;
        this.scopeFactory = scopeFactory;
        config = options.Value;
        this.logger = logger;
    }

    // Compatibility constructor for isolated callers that supply a fake provider.
    public ImdbImportService(
        ImdbTsvProvider parser,
        IImdbImportProvider importProvider,
        IOptions<ImdbImportOptions> options,
        ILogger<ImdbImportService> logger)
    {
        this.parser = parser;
        providerOverride = importProvider;
        config = options.Value;
        this.logger = logger;
    }

    public Task<ImdbImportRunResult> ImportAsync(CancellationToken ct = default) =>
        ImportAsync(new ImdbImportExecution(config), ct);

    public async Task<ImdbImportRunResult> ImportAsync(ImdbImportExecution execution, CancellationToken ct = default)
    {
        config.ValidateFiniteProfile();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(config.MaxWholeSessionSeconds));
        var runCt = deadline.Token;
        logger.LogInformation("Starting IMDb import invocation.");

        execution.SetStage("database-cutoff");
        var cutoff = await WithProviderAsync(p => p.GetDatabaseUtcNowAsync(runCt), runCt);
        execution.SetStage("ratings");
        var ratings = await ImportRatingsAsync(execution, runCt);
        execution.SetStage("basics");
        var basics = await ImportBasicsAsync(execution, runCt);
        execution.SetStage("episodes");
        var episodes = await ImportEpisodesAsync(execution, runCt);

        execution.SetStage("cleanup:stale-ratings");
        await CleanupAsync(cutoff, execution, runCt);
        execution.SetStage("import-complete");
        logger.LogInformation("IMDb feeds completed. Rows read: {RowsRead}, rows affected: {RowsAffected}, HTTP attempts: {HttpAttempts}.",
            execution.Counters.RowsRead, execution.Counters.RowsAffected, execution.HttpAttempts);

        return new ImdbImportRunResult(
            basics,
            episodes,
            ratings,
            RatingsSucceeded: true,
            Completed: true,
            Counters: execution.Counters,
            RatingsCutoffUtc: cutoff);
    }

    private async Task<ImdbImportResult> ImportRatingsAsync(ImdbImportExecution execution, CancellationToken ct)
    {
        var affected = 0;
        var skipped = 0;
        await parser.RunBatchImportAsync(
            config.RatingsDatasetUrl,
            RatingsHeaders,
            ParseRatingsRow,
            async (batch, callbackCt) =>
            {
                var result = await WithProviderAsync(p => p.ImportRatingsAsync(batch, callbackCt), callbackCt);
                affected += result.Affected;
                skipped += result.Skipped;
                execution.Counters.AddBatch(result);
            },
            execution,
            ct);

        if (affected == 0)
            throw new InvalidDataException("IMDb ratings feed produced no affected rows.");
        return new ImdbImportResult(affected, skipped);
    }

    private async Task<ImdbImportResult> ImportBasicsAsync(ImdbImportExecution execution, CancellationToken ct)
    {
        var affected = 0;
        var skipped = 0;
        await parser.RunBatchImportAsync(
            config.DatasetUrl,
            BasicsHeaders,
            ParseBasicsRow,
            async (batch, callbackCt) =>
            {
                var result = await WithProviderAsync(p => p.ImportBasicsAsync(batch, callbackCt), callbackCt);
                affected += result.Affected;
                skipped += result.Skipped;
                execution.Counters.AddBatch(result);
            },
            execution,
            ct);
        return new ImdbImportResult(affected, skipped);
    }

    private async Task<ImdbImportResult> ImportEpisodesAsync(ImdbImportExecution execution, CancellationToken ct)
    {
        var affected = 0;
        var skipped = 0;
        await parser.RunBatchImportAsync(
            config.EpisodesDatasetUrl,
            EpisodeHeaders,
            ParseEpisodeRow,
            async (batch, callbackCt) =>
            {
                var result = await WithProviderAsync(p => p.ImportEpisodesAsync(batch, callbackCt), callbackCt);
                affected += result.Affected;
                skipped += result.Skipped;
                execution.Counters.AddBatch(result);
            },
            execution,
            ct);
        return new ImdbImportResult(affected, skipped);
    }

    private async Task CleanupAsync(DateTimeOffset cutoff, ImdbImportExecution execution, CancellationToken ct)
    {
        await DrainCleanupAsync("stale-ratings", p => p.DeleteStaleRatingsAsync(cutoff, config.MaxCleanupRowsPerUnit, ct), execution, ct);
        await DrainCleanupAsync("future", p => p.DeleteFutureImportsAsync(config.MaxCleanupRowsPerUnit, ct), execution, ct);
        await DrainCleanupAsync("tv-pilot", p => p.DeleteTvPilotImportsAsync(config.MaxCleanupRowsPerUnit, ct), execution, ct);
        await DrainOrphanEpisodesAsync(execution, ct);
    }

    private async Task DrainOrphanEpisodesAsync(ImdbImportExecution execution, CancellationToken ct)
    {
        execution.SetStage("cleanup:orphan-episodes");
        long? afterId = null;
        while (true)
        {
            var batch = await WithProviderAsync(
                p => p.DeleteOrphanEpisodesBatchAsync(afterId, config.MaxCleanupRowsPerUnit, ct), ct);
            execution.Counters.AddCleanup(batch.Affected);
            if (!batch.HasMore) return;
            if (batch.NextId is null || (afterId is not null && batch.NextId <= afterId))
                throw new InvalidDataException("IMDb orphan episode cleanup returned non-advancing page progress.");

            afterId = batch.NextId;
            logger.LogInformation("IMDb cleanup stage {Stage} affected {Count} rows; continuing after episode id {NextId}.",
                "orphan-episodes", batch.Affected, afterId);
            if (config.YieldBetweenUnitsMilliseconds > 0)
                await Task.Delay(config.YieldBetweenUnitsMilliseconds, ct);
        }
    }

    private async Task DrainCleanupAsync(
        string stage,
        Func<IImdbImportProvider, Task<int>> operation,
        ImdbImportExecution execution,
        CancellationToken ct)
    {
        execution.SetStage($"cleanup:{stage}");
        while (true)
        {
            var affected = await WithProviderAsync(operation, ct);
            execution.Counters.AddCleanup(affected);
            if (affected == 0) return;
            logger.LogInformation("IMDb cleanup stage {Stage} affected {Count} rows.", stage, affected);
            if (config.YieldBetweenUnitsMilliseconds > 0)
                await Task.Delay(config.YieldBetweenUnitsMilliseconds, ct);
        }
    }

    private async Task<T> WithProviderAsync<T>(Func<IImdbImportProvider, Task<T>> operation, CancellationToken ct)
    {
        if (providerOverride is not null) return await operation(providerOverride);
        using var scope = scopeFactory!.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<IImdbImportProvider>());
    }

    private static ImdbTsvRow? ParseBasicsRow(string[] columns, long lineNumber, string line)
    {
        RequireTconst(columns[0], "basics", lineNumber);
        RequireText(columns[1], "basics", lineNumber);
        RequireText(columns[2], "basics", lineNumber);
        RequireText(columns[3], "basics", lineNumber);
        var adult = columns[4] switch
        {
            "0" => false,
            "1" => true,
            _ => throw InvalidRow("basics", lineNumber)
        };

        var startYear = ParseNullableInt(columns[5], "basics", lineNumber);
        var endYear = ParseNullableInt(columns[6], "basics", lineNumber);
        var runtime = ParseNullableInt(columns[7], "basics", lineNumber);
        if (adult || columns[1] == "videoGame") return null;

        return new ImdbTsvRow(columns[0], columns[1], SanitizeTitle(columns[2]), SanitizeTitle(columns[3]), adult,
            startYear, endYear, runtime, columns[8] == @"\N" ? null : columns[8], line);
    }

    private static ImdbEpisodeTsvRow ParseEpisodeRow(string[] columns, long lineNumber, string line)
    {
        RequireTconst(columns[0], "episodes", lineNumber);
        RequireTconst(columns[1], "episodes", lineNumber);
        return new ImdbEpisodeTsvRow(columns[0], columns[1], ParseEpisodeNumber(columns[2], lineNumber), ParseEpisodeNumber(columns[3], lineNumber), line);
    }

    private static ImdbRatingTsvRow ParseRatingsRow(string[] columns, long lineNumber, string line)
    {
        RequireTconst(columns[0], "ratings", lineNumber);
        if (!decimal.TryParse(columns[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rating) || rating is < 0 or > 10)
            throw InvalidRow("ratings", lineNumber);
        if (!int.TryParse(columns[2], NumberStyles.None, CultureInfo.InvariantCulture, out var votes) || votes < 0)
            throw InvalidRow("ratings", lineNumber);
        return new ImdbRatingTsvRow(columns[0], rating, votes, line);
    }

    private static int ParseEpisodeNumber(string value, long lineNumber) => value switch
    {
        @"\N" => -1,
        _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) => result,
        _ => throw InvalidRow("episodes", lineNumber)
    };

    private static int? ParseNullableInt(string value, string feed, long lineNumber)
    {
        if (value == @"\N") return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)) return result;
        throw InvalidRow(feed, lineNumber);
    }

    private static void RequireTconst(string value, string feed, long lineNumber)
    {
        if (value.Length < 3 || !value.StartsWith("tt", StringComparison.Ordinal) || !value[2..].All(char.IsDigit))
            throw InvalidRow(feed, lineNumber);
    }

    private static void RequireText(string value, string feed, long lineNumber)
    {
        if (string.IsNullOrWhiteSpace(value) || value == @"\N") throw InvalidRow(feed, lineNumber);
    }

    private static InvalidDataException InvalidRow(string feed, long lineNumber) =>
        new($"IMDb {feed} feed contained an invalid required value at line {lineNumber}.");

    private static string SanitizeTitle(string title) => title.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal);
}
