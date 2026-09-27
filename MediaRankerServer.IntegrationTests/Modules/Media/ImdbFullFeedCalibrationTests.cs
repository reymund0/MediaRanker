using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

/// <summary>Opt-in full-size replay from already retained IMDb feeds, isolated to the fixture database.</summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class ImdbFullFeedCalibrationTests(PostgresContainerFixture postgresFixture) : IAsyncLifetime
{
    private const int MeasuredStatementTimeoutSeconds = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private CalibrationConfig? config;
    private readonly CalibrationProgress progress = new();
    private readonly SemaphoreSlim artifactLock = new(1, 1);
    private DateTimeOffset startedAt;
    private DateTimeOffset executionDeadlineUtc;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("IMDB_FULL_CALIBRATION") == "1")
        {
            var directory = Environment.GetEnvironmentVariable("IMDB_CALIBRATION_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory) && Directory.Exists(directory))
            {
                // Publish fixture identity before migrations so the parent guard can attach immediately.
                var root = Path.GetFullPath(directory);
                await WriteArtifactAsync(Path.Combine(root, "runtime.json"), new
                {
                    processId = Environment.ProcessId,
                    containerId = postgresFixture.Container.Id,
                    status = "fixture-ready",
                    startedAtUtc = DateTimeOffset.UtcNow
                });
                if (!await WaitForGuardReadyAsync(root, postgresFixture.Container.Id))
                    throw new InvalidOperationException("IMDb calibration stopped before migration (guard-ready-timeout).");
            }
        }

        await using var db = new PostgreSQLContext(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(postgresFixture.GetConnectionString(), options => options.CommandTimeout(MeasuredStatementTimeoutSeconds))
            .UseSnakeCaseNamingConvention()
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false)
            .Options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("{literal}")]
    [InlineData("{0}")]
    [InlineData("{{literal}}")]
    [InlineData("left{")]
    [InlineData("right}")]
    [InlineData("a'b {0}")]
    public async Task ImportBasics_PreservesLiteralBracesInRetainedSource(string sourceText)
    {
        await using var db = new PostgreSQLContext(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(postgresFixture.GetConnectionString())
            .UseSnakeCaseNamingConvention().Options);
        var provider = new ImdbImportSqlProvider(db, NullLogger<ImdbImportSqlProvider>.Instance);
        var raw = "tt999999999\tmovie\tA {literal} title\tA {literal} title\t0\t2020\t\\N\t90\tDrama" + sourceText;
        await provider.ImportBasicsAsync([new ImdbTsvRow("tt999999999", "movie", "A literal title",
            "A literal title", false, 2020, null, 90, "Drama", raw)], CancellationToken.None);
        var stored = await db.Database.SqlQueryRaw<string>(
            "SELECT raw_line AS \"Value\" FROM imdb_imports WHERE tconst = 'tt999999999'").SingleAsync();
        Assert.Equal(raw, stored);
    }

    [Fact]
    public async Task OptInCalibration_ReplaysOnlyRetainedLocalFeeds()
    {
        if (Environment.GetEnvironmentVariable("IMDB_FULL_CALIBRATION") != "1") return;

        config = CalibrationConfig.Read();
        startedAt = DateTimeOffset.UtcNow;
        var remaining = config.DeadlineUtc - startedAt;
        if (remaining <= TimeSpan.Zero) throw new InvalidOperationException("IMDb calibration deadline has expired.");
        var sessionWindow = Min(TimeSpan.FromMinutes(90), remaining);
        executionDeadlineUtc = startedAt + sessionWindow;
        using var deadline = new CancellationTokenSource(sessionWindow);
        progress.ProcessId = Environment.ProcessId;
        progress.ContainerId = postgresFixture.Container.Id;
        progress.StartedAtUtc = startedAt;
        progress.DeadlineUtc = config.DeadlineUtc;
        progress.RequestedPhase = config.Phase;
        progress.Stage = "preflight";
        await SaveProgressAsync();

        using var monitorStop = new CancellationTokenSource();
        var monitor = MonitorAsync(deadline, monitorStop.Token);
        string failedStage = "preflight";
        Exception? originalFailure = null;
        var terminal = new Dictionary<string, object?>();

        try
        {
            ValidateRetainedFiles(config);
            var tempDirectory = ValidateIsolatedTemp(config);
            if (File.Exists(config.ReplayApprovalPath))
                throw new InvalidOperationException("Replay approval file must be created only after first-success.json is written.");

            progress.Stage = "import";
            await SaveProgressAsync();
            failedStage = progress.Stage;
            var first = await RunImportAndLoadAsync(config, tempDirectory, deadline.Token);
            progress.FirstPass = first;
            terminal["firstPass"] = first;
            progress.Stage = "first-pass-complete";
            progress.LastDatabaseSizeBytes = first.Database.DatabaseSizeBytes;
            await WriteArtifactAsync(config.FirstSuccessPath, new
            {
                status = "completed",
                processId = Environment.ProcessId,
                containerId = postgresFixture.Container.Id,
                completedAtUtc = DateTimeOffset.UtcNow,
                first
            });
            await SaveProgressAsync();

            var approvalWait = Min(TimeSpan.FromMinutes(5), executionDeadlineUtc - DateTimeOffset.UtcNow);
            progress.Stage = "awaiting-replay-approval";
            await SaveProgressAsync();
            var approved = await WaitForReplayApprovalAsync(approvalWait, deadline.Token);
            if (!approved)
            {
                terminal["status"] = "first-pass-complete-replay-not-approved";
                terminal["replayStatus"] = "not-run";
                terminal["replayWaitSeconds"] = approvalWait.TotalSeconds;
            }
            else
            {
                progress.Stage = "cancellation-validation";
                await SaveProgressAsync();
                failedStage = progress.Stage;
                var cancellation = await ValidateCancellationAsync(config, tempDirectory, deadline.Token);
                progress.CancellationValidation = cancellation;
                terminal["cancellationValidation"] = cancellation;

                progress.Stage = "replay-import";
                await SaveProgressAsync();
                failedStage = progress.Stage;
                var replay = await RunImportAndLoadAsync(config, tempDirectory, deadline.Token);
                progress.Replay = replay;
                terminal["replay"] = replay;
                progress.LastDatabaseSizeBytes = replay.Database.DatabaseSizeBytes;
                terminal["status"] = "completed";
                terminal["replayStatus"] = "completed";
            }
        }
        catch (Exception ex)
        {
            originalFailure = ex;
            failedStage = progress.Stage ?? failedStage;
            terminal["status"] = deadline.IsCancellationRequested ? "stopped-by-deadline-or-resource-guard" : "failed";
            terminal["failure"] = new { stage = failedStage, category = SafeCategory(ex), detail = System.Text.RegularExpressions.Regex.IsMatch(ex.Message, @"^IMDb (basics|episodes|ratings) feed contained an invalid required value at line [0-9]+\.$") ? ex.Message : null };
            progress.FailureStage = failedStage;
            progress.FailureCategory = SafeCategory(ex);
        }
        finally
        {
            monitorStop.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
            terminal["processId"] = Environment.ProcessId;
            terminal["containerId"] = postgresFixture.Container.Id;
            terminal["startedAtUtc"] = startedAt;
            terminal["finishedAtUtc"] = DateTimeOffset.UtcNow;
            terminal["elapsedSeconds"] = Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalSeconds);
            terminal["deadlineUtc"] = config.DeadlineUtc;
            terminal["peakWorkingSetBytes"] = progress.PeakWorkingSetBytes;
            terminal["peakTemporaryBytes"] = progress.PeakTemporaryBytes;
            terminal.TryAdd("failure", null);
            progress.Stage = originalFailure is null ? "complete" : "failed";
            progress.FinishedAtUtc = DateTimeOffset.UtcNow;
            try
            {
                await SaveProgressAsync();
                await WriteArtifactAsync(config.ResultPath, terminal);
            }
            catch when (originalFailure is not null)
            {
                // Preserve the sanitized test failure if the filesystem also rejects final artifact writes.
            }
        }

        if (originalFailure is not null)
            throw new InvalidOperationException($"IMDb calibration stopped during '{failedStage}' ({SafeCategory(originalFailure)}); see sanitized result.json.");
    }

    private async Task<CalibrationRun> RunImportAndLoadAsync(CalibrationConfig runConfig, string tempDirectory, CancellationToken ct)
    {
        var options = CreateImportOptions(runConfig.DeadlineUtc);
        options.ValidateFiniteProfile();
        var handler = new RetainedFileHandler(runConfig.FeedPaths);
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var instrumentation = new BulkCatalogDbInstrumentation();
        using var logs = new SafeCalibrationLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var services = BuildProvider(postgresFixture.GetConnectionString(), options, instrumentation, loggerFactory);
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var parser = new ImdbTsvProvider(http, Options.Create(options), loggerFactory.CreateLogger<ImdbTsvProvider>());
        var importer = new ImdbImportService(parser, scopes, Options.Create(options), loggerFactory.CreateLogger<ImdbImportService>());
        var loader = new ImdbLoadService(scopes, Options.Create(options), loggerFactory.CreateLogger<ImdbLoadService>());

        var start = Stopwatch.GetTimestamp();
        var importStarted = Stopwatch.GetTimestamp();
        progress.Stage = "import-feeds-and-cleanup";
        await SaveProgressAsync();
        var imported = await importer.ImportAsync(ct);
        var importMilliseconds = ElapsedMilliseconds(importStarted);
        if (!imported.Completed || imported.FeedBlocked || !imported.RatingsSucceeded)
            throw new InvalidOperationException("IMDb importer did not complete all feeds.");

        var loadStarted = Stopwatch.GetTimestamp();
        progress.Stage = "load-catalog";
        progress.AggregateImportCounts = new ImportCounts(imported.Counters?.RowsRead, imported.Counters?.ParsedRows,
            imported.Counters?.FilteredRows, imported.Counters?.BatchesCommitted, imported.Counters?.RowsAffected,
            imported.Counters?.RowsSkipped, imported.Counters?.CleanupRowsAffected);
        await SaveProgressAsync();
        var loaded = await loader.LoadAsync(ct);
        var loadMilliseconds = ElapsedMilliseconds(loadStarted);
        var database = await ReadDatabaseSnapshotAsync(postgresFixture.GetConnectionString(), ct);
        var requests = handler.Snapshot();
        if (requests.Any(feed => feed.RequestCount != 1) || requests.Count != 3)
            throw new InvalidOperationException("Local IMDb replay did not make exactly one request for each retained feed.");

        return new CalibrationRun(
            MeasuredStatementTimeoutSeconds,
            Math.Max(0, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds),
            importMilliseconds,
            loadMilliseconds,
            imported.RatingsCutoffUtc,
            new ImportCounts(imported.Counters?.RowsRead, imported.Counters?.ParsedRows, imported.Counters?.FilteredRows,
                imported.Counters?.BatchesCommitted, imported.Counters?.RowsAffected, imported.Counters?.RowsSkipped,
                imported.Counters?.CleanupRowsAffected),
            new FeedResults(
                imported.Ratings.Affected, imported.Ratings.Skipped,
                imported.Basics.Affected, imported.Basics.Skipped,
                imported.Episodes?.Affected, imported.Episodes?.Skipped,
                requests.Select(feed => new FeedTransfer(feed.FileName, feed.RequestCount, feed.BytesServed,
                    RowsRead: null, DecompressedBytes: null)).ToArray()),
            loaded.Affected,
            database,
            new SqlSummary(
                instrumentation.Commands.Count,
                instrumentation.Commands.Count(command => !command.Succeeded),
                instrumentation.Commands.GroupBy(command => command.Kind).ToDictionary(group => group.Key, group => group.Count()),
                instrumentation.TransactionsStarted,
                instrumentation.MaximumTransactionDurationMilliseconds,
                logs.SqlErrorCategories.ToArray()),
            logs.LoadStageMilliseconds,
            "Per-feed decompressed-byte and parser-row counters are not exposed by the current importer; aggregate parser counters are recorded.");
    }

    private async Task<CancellationSummary> ValidateCancellationAsync(CalibrationConfig runConfig, string tempDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var options = CreateImportOptions(runConfig.DeadlineUtc);
        var handler = new RetainedFileHandler(runConfig.FeedPaths);
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var instrumentation = new BulkCatalogDbInstrumentation();
        using var logs = new SafeCalibrationLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var services = BuildProvider(postgresFixture.GetConnectionString(), options, instrumentation, loggerFactory);
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var parser = new ImdbTsvProvider(http, Options.Create(options), loggerFactory.CreateLogger<ImdbTsvProvider>());
        var importer = new ImdbImportService(parser, scopes, Options.Create(options), loggerFactory.CreateLogger<ImdbImportService>());
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(ct);
        handler.CancelAfterFirstRead = canceled;
        try
        {
            await importer.ImportAsync(canceled.Token);
            throw new InvalidOperationException("IMDb importer unexpectedly accepted a canceled token.");
        }
        catch (OperationCanceledException)
        {
            var sends = handler.Snapshot().Sum(feed => feed.RequestCount);
            Assert.Equal(1, sends);
            Assert.Equal(0, GetOwnedTemporaryBytes(tempDirectory));
            return new CancellationSummary("in-flight-feed-copy-canceled-and-temporary-file-removed", sends);
        }
    }

    private ServiceProvider BuildProvider(
        string connectionString,
        ImdbImportOptions options,
        BulkCatalogDbInstrumentation instrumentation,
        ILoggerFactory loggerFactory)
    {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IOptions<ImdbImportOptions>>(Options.Create(options));
        registrations.AddSingleton(loggerFactory);
        registrations.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        registrations.AddScoped(_ => new PostgreSQLContext(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(connectionString, pg => pg.CommandTimeout(MeasuredStatementTimeoutSeconds))
            .UseSnakeCaseNamingConvention()
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false)
            .AddInterceptors(instrumentation, instrumentation.TransactionInstrumentation)
            .Options));
        registrations.AddScoped<IImdbImportProvider, ImdbImportSqlProvider>();
        registrations.AddScoped<IImdbLoadProvider, ImdbLoadSqlProvider>();
        return registrations.BuildServiceProvider(validateScopes: true);
    }

    private static ImdbImportOptions CreateImportOptions(DateTimeOffset deadlineUtc)
    {
        var secondsRemaining = Math.Max(1, (int)Math.Floor((deadlineUtc - DateTimeOffset.UtcNow).TotalSeconds));
        return new ImdbImportOptions
        {
            Enabled = false,
            CalibratedProfileConfirmed = false,
            MaxHttpAttempts = 3,
            MaxCompressedBytesPerFeed = 2L * 1024 * 1024 * 1024,
            MaxTemporaryDiskBytes = 2L * 1024 * 1024 * 1024,
            MaxDecompressedBytesPerFeed = 20L * 1024 * 1024 * 1024,
            MaxWholeSessionSeconds = Math.Min(90 * 60, secondsRemaining),
            MaxRowsPerFeed = 50_000_000,
            MaxLineCharacters = 1_048_576,
            MaxBatchCharacters = 8 * 1024 * 1024,
            BatchSize = 5_000,
            MaxCleanupRowsPerUnit = 1_000,
            MaxLoadRowsPerUnit = 1_000,
            MaxStatementSeconds = MeasuredStatementTimeoutSeconds,
            YieldBetweenUnitsMilliseconds = 0
        };
    }

    private async Task<bool> WaitForReplayApprovalAsync(TimeSpan wait, CancellationToken ct)
    {
        if (wait <= TimeSpan.Zero) return false;
        var stopAt = DateTimeOffset.UtcNow + wait;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(config!.ReplayApprovalPath)) return true;
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            progress.ReplayWaitSeconds = (long)Math.Max(0, wait.TotalSeconds - (stopAt - DateTimeOffset.UtcNow).TotalSeconds);
            await SaveProgressAsync();
        }
        return File.Exists(config!.ReplayApprovalPath);
    }

    private async Task<bool> WaitForGuardReadyAsync(string directory, string containerId)
    {
        var deadlineText = Environment.GetEnvironmentVariable("IMDB_CALIBRATION_DEADLINE_UTC");
        if (!DateTimeOffset.TryParse(deadlineText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var globalDeadline) ||
            globalDeadline.Offset != TimeSpan.Zero || globalDeadline <= DateTimeOffset.UtcNow)
            return false;

        var wait = Min(TimeSpan.FromSeconds(30), globalDeadline - DateTimeOffset.UtcNow);
        var readyPath = Path.Combine(directory, "guard-ready.json");
        var notBeforeUtc = DateTimeOffset.UtcNow;
        var stopAt = DateTimeOffset.UtcNow + wait;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            if (File.Exists(readyPath) && File.GetLastWriteTimeUtc(readyPath) >= notBeforeUtc.UtcDateTime)
            {
                try
                {
                    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(readyPath));
                    var root = document.RootElement;
                    if (root.TryGetProperty("containerId", out var id) && id.GetString() == containerId &&
                        root.TryGetProperty("status", out var status) && status.GetString() == "ready")
                        return true;
                }
                catch (JsonException) { }
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        return false;
    }

    private async Task MonitorAsync(CancellationTokenSource deadline, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var workingSet = Process.GetCurrentProcess().WorkingSet64;
            progress.PeakWorkingSetBytes = Math.Max(progress.PeakWorkingSetBytes, workingSet);
            var tempBytes = GetOwnedTemporaryBytes(config?.TempDirectory);
            progress.PeakTemporaryBytes = Math.Max(progress.PeakTemporaryBytes, tempBytes);
            progress.CurrentWorkingSetBytes = workingSet;
            progress.CurrentTemporaryBytes = tempBytes;
            if (workingSet > CalibrationConfig.MaxWorkingSetBytes)
            {
                progress.ResourceStopReason = "process-rss-limit";
                deadline.Cancel();
            }
            if (tempBytes > CalibrationConfig.MaxTemporaryBytes ||
                tempBytes + (config?.RetainedBytes ?? 0) > CalibrationConfig.MaxCombinedDiskBytes)
            {
                progress.ResourceStopReason = "temporary-disk-limit";
                deadline.Cancel();
            }
            try { await SaveProgressAsync(); }
            catch { /* A heartbeat failure must not replace an importer or SQL failure. */ }
            await Task.Delay(TimeSpan.FromSeconds(5), stop);
        }
    }

    private async Task SaveProgressAsync()
    {
        if (config is null) return;
        await WriteArtifactAsync(config.ProgressPath, progress);
    }

    private async Task WriteArtifactAsync(string path, object payload)
    {
        await artifactLock.WaitAsync();
        try
        {
            var temporaryPath = path + ".writing";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(payload, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally { artifactLock.Release(); }
    }

    private static async Task<DatabaseSnapshot> ReadDatabaseSnapshotAsync(string connectionString, CancellationToken outerToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(MeasuredStatementTimeoutSeconds));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = MeasuredStatementTimeoutSeconds;
        command.CommandText = """
            SELECT pg_database_size(current_database()),
                   (SELECT count(*) FROM imdb_imports),
                   (SELECT count(*) FROM imdb_import_ratings),
                   (SELECT count(*) FROM imdb_import_episodes),
                   (SELECT count(*) FROM media),
                   (SELECT count(*) FROM media_collections)
            """;
        await using var reader = await command.ExecuteReaderAsync(timeout.Token);
        await reader.ReadAsync(timeout.Token);
        return new DatabaseSnapshot(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5));
    }

    private static void ValidateRetainedFiles(CalibrationConfig runConfig)
    {
        long combined = 0;
        foreach (var file in runConfig.FeedPaths.Values)
        {
            var size = new FileInfo(file).Length;
            if (size <= 0 || size > CalibrationConfig.MaxCompressedBytesPerFeed)
                throw new InvalidOperationException("A retained IMDb feed is empty or exceeds the compressed-byte limit.");
            combined = checked(combined + size);
        }
        if (combined > CalibrationConfig.MaxRetainedBytes)
            throw new InvalidOperationException("Retained IMDb feeds exceed the aggregate compressed-byte limit.");
        runConfig.RetainedBytes = combined;
    }

    private static string ValidateIsolatedTemp(CalibrationConfig runConfig)
    {
        var temp = Environment.GetEnvironmentVariable("TEMP");
        var tmp = Environment.GetEnvironmentVariable("TMP");
        if (string.IsNullOrWhiteSpace(temp) || string.IsNullOrWhiteSpace(tmp) ||
            !Path.GetFullPath(temp).Equals(Path.GetFullPath(tmp), StringComparison.OrdinalIgnoreCase) ||
            !Path.TrimEndingDirectorySeparator(Path.GetFullPath(temp)).Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(temp) || Path.GetFullPath(temp).Equals(runConfig.Directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("TEMP and TMP must point to the same existing isolated directory used by Path.GetTempPath().");

        runConfig.TempDirectory = Path.GetFullPath(temp);
        if (Directory.EnumerateFiles(runConfig.TempDirectory, "mediaranker-imdb-*.gz", SearchOption.TopDirectoryOnly).Any())
            throw new InvalidOperationException("Isolated temporary directory contains a prior IMDb download copy.");
        return runConfig.TempDirectory;
    }

    private static long GetOwnedTemporaryBytes(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return 0;
        return Directory.EnumerateFiles(directory, "mediaranker-imdb-*.gz", SearchOption.TopDirectoryOnly)
            .Sum(path => new FileInfo(path).Length);
    }

    private static long ElapsedMilliseconds(long start) => Math.Max(0, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static string SafeCategory(Exception ex) => ex.GetType().Name;
}

internal sealed class CalibrationConfig
{
    public const long MaxCompressedBytesPerFeed = 2L * 1024 * 1024 * 1024;
    public const long MaxRetainedBytes = 6L * 1024 * 1024 * 1024;
    public const long MaxTemporaryBytes = 2L * 1024 * 1024 * 1024;
    public const long MaxCombinedDiskBytes = 8L * 1024 * 1024 * 1024;
    public const long MaxWorkingSetBytes = 2L * 1024 * 1024 * 1024;

    private CalibrationConfig(string directory, DateTimeOffset deadlineUtc, string phase)
    {
        Directory = directory;
        DeadlineUtc = deadlineUtc;
        Phase = phase;
        FeedPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title.ratings.tsv.gz"] = Path.Combine(directory, "title.ratings.tsv.gz"),
            ["title.basics.tsv.gz"] = Path.Combine(directory, "title.basics.tsv.gz"),
            ["title.episode.tsv.gz"] = Path.Combine(directory, "title.episode.tsv.gz")
        };
        ProgressPath = Path.Combine(directory, "progress.json");
        ResultPath = Path.Combine(directory, "result.json");
        FirstSuccessPath = Path.Combine(directory, "first-success.json");
        ReplayApprovalPath = Path.Combine(directory, "replay.approved");
    }

    public string Directory { get; }
    public DateTimeOffset DeadlineUtc { get; }
    public string Phase { get; }
    public Dictionary<string, string> FeedPaths { get; }
    public string ProgressPath { get; }
    public string ResultPath { get; }
    public string FirstSuccessPath { get; }
    public string ReplayApprovalPath { get; }
    public long RetainedBytes { get; set; }
    public string? TempDirectory { get; set; }

    public static CalibrationConfig Read()
    {
        var directory = Environment.GetEnvironmentVariable("IMDB_CALIBRATION_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) || !System.IO.Directory.Exists(directory))
            throw new InvalidOperationException("IMDB_CALIBRATION_DIRECTORY must be an existing absolute directory.");
        if (!DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("IMDB_CALIBRATION_DEADLINE_UTC"), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var deadline) || deadline.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("IMDB_CALIBRATION_DEADLINE_UTC must be a UTC timestamp with an explicit zero offset.");
        var phase = Environment.GetEnvironmentVariable("IMDB_CALIBRATION_PHASE") ?? "first";
        if (phase is not ("first" or "replay")) throw new InvalidOperationException("IMDB_CALIBRATION_PHASE must be first or replay.");
        return new CalibrationConfig(Path.GetFullPath(directory), deadline, phase);
    }
}

internal sealed class CalibrationProgress
{
    public int ProcessId { get; set; }
    public string? ContainerId { get; set; }
    public string? RequestedPhase { get; set; }
    public string? Stage { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public long CurrentWorkingSetBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long CurrentTemporaryBytes { get; set; }
    public long PeakTemporaryBytes { get; set; }
    public long? LastDatabaseSizeBytes { get; set; }
    public long ReplayWaitSeconds { get; set; }
    public string? ResourceStopReason { get; set; }
    public string? FailureStage { get; set; }
    public string? FailureCategory { get; set; }
    public CalibrationRun? FirstPass { get; set; }
    public CalibrationRun? Replay { get; set; }
    public CancellationSummary? CancellationValidation { get; set; }
    public ImportCounts? AggregateImportCounts { get; set; }
}

internal sealed record CalibrationRun(
    int StatementTimeoutSeconds,
    long ElapsedMilliseconds,
    long ImportMilliseconds,
    long LoadMilliseconds,
    DateTimeOffset? CleanupCutoffUtc,
    ImportCounts ImportCounts,
    FeedResults FeedResults,
    int LoadRowsAffected,
    DatabaseSnapshot Database,
    SqlSummary Sql,
    IReadOnlyDictionary<string, long> LoadStageMilliseconds,
    string PerFeedCoverage);

internal sealed record ImportCounts(long? RowsRead, long? ParsedRows, long? FilteredRows, long? BatchesCommitted,
    long? RowsAffected, long? RowsSkipped, long? CleanupRowsAffected);
internal sealed record FeedResults(int RatingsAffected, int RatingsSkipped, int BasicsAffected, int BasicsSkipped,
    int? EpisodesAffected, int? EpisodesSkipped, IReadOnlyList<FeedTransfer> Transfers);
internal sealed record FeedTransfer(string FileName, int RequestCount, long CompressedBytesServed, long? RowsRead, long? DecompressedBytes);
internal sealed record DatabaseSnapshot(long DatabaseSizeBytes, long ImportRows, long RatingRows, long EpisodeRows, long MediaRows, long CollectionRows);
internal sealed record SqlSummary(int Commands, int Failures, IReadOnlyDictionary<string, int> CommandsByKind,
    int Transactions, long MaximumTransactionMilliseconds, IReadOnlyList<string> SqlErrorCategories);
internal sealed record CancellationSummary(string Result, int FeedRequests);

internal sealed record FeedTransferSnapshot(string FileName, int RequestCount, long BytesServed);

internal sealed class RetainedFileHandler(IReadOnlyDictionary<string, string> feedPaths) : HttpMessageHandler
{
    public CancellationTokenSource? CancelAfterFirstRead { get; set; }
    private readonly ConcurrentDictionary<string, MutableFeedTransfer> transfers = new(StringComparer.Ordinal);

    public IReadOnlyList<FeedTransferSnapshot> Snapshot() => transfers.Select(pair =>
        new FeedTransferSnapshot(pair.Key, pair.Value.RequestCount, pair.Value.BytesServed)).ToArray();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = request.RequestUri;
        if (request.Method != HttpMethod.Get || uri is null || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host != "datasets.imdbws.com" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new HttpRequestException("IMDb calibration handler rejected an unexpected request.");

        var name = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (name.Contains('/') || !feedPaths.TryGetValue(name, out var path) || !File.Exists(path))
            throw new HttpRequestException("IMDb calibration handler rejected an unknown retained feed.");

        var size = new FileInfo(path).Length;
        var transfer = transfers.GetOrAdd(name, _ => new MutableFeedTransfer());
        Interlocked.Increment(ref transfer.RequestCount);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var content = new StreamContent(new CountingReadStream(stream, bytes => { Interlocked.Add(ref transfer.BytesServed, bytes); CancelAfterFirstRead?.Cancel(); }));
        content.Headers.ContentLength = size;
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
        return Task.FromResult(response);
    }

    private sealed class MutableFeedTransfer
    {
        public int RequestCount;
        public long BytesServed;
    }

    private sealed class CountingReadStream(Stream inner, Action<int> onRead) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            if (read > 0) onRead(read);
            return read;
        }
        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            if (read > 0) onRead(read);
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            if (read > 0) onRead(read);
            return read;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            if (read > 0) onRead(read);
            return read;
        }
        public override int ReadByte()
        {
            var value = inner.ReadByte();
            if (value >= 0) onRead(1);
            return value;
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

internal sealed class SafeCalibrationLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> errorCategories = new();
    private readonly ConcurrentDictionary<string, long> stageStarts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> stageDurations = new(StringComparer.Ordinal);
    public IReadOnlyList<string> SqlErrorCategories => errorCategories.ToArray();
    public IReadOnlyDictionary<string, long> LoadStageMilliseconds => new Dictionary<string, long>(stageDurations);
    public ILogger CreateLogger(string categoryName) => new SafeLogger(categoryName, this);
    public void Dispose() { }

    private void Capture<TState>(string category, LogLevel level, TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> fields) return;
        var values = fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
        var template = values.TryGetValue("{OriginalFormat}", out var original) ? original?.ToString() : null;
        if (level == LogLevel.Error && values.TryGetValue("ErrorCategory", out var errorCategory) && errorCategory is not null)
            errorCategories.Enqueue(errorCategory.ToString() ?? "unknown");
        if (!category.EndsWith("ImdbLoadService", StringComparison.Ordinal)) return;
        if (values.TryGetValue("Stage", out var stageValue) && stageValue is string stage)
        {
            if (template?.StartsWith("Starting IMDb load stage", StringComparison.Ordinal) == true)
                stageStarts[stage] = Stopwatch.GetTimestamp();
            else if (template?.StartsWith("IMDb load stage", StringComparison.Ordinal) == true && stageStarts.TryRemove(stage, out var start))
                stageDurations[stage] = Math.Max(0, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    private sealed class SafeLogger(string category, SafeCalibrationLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => owner.Capture(category, logLevel, state);
    }
}
