using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

/// <summary>
/// Opt-in controls for the isolated bulk-catalog measurement harness. The harness
/// intentionally has no useful default: a normal integration-test run must not
/// allocate 50,000-row fixtures or write a result file.
/// </summary>
internal sealed record BulkCatalogMeasurementOptions(
    string OutputPath,
    string Phase,
    string Profile,
    int Samples,
    int EndpointRepetitions,
    IReadOnlyList<int> StagingSizes,
    IReadOnlyList<int> EligiblePercentages,
    IReadOnlyList<string> States)
{
    public static BulkCatalogMeasurementOptions? FromEnvironment()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE"), "1", StringComparison.Ordinal))
            return null;

        var output = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("MEDIARANKER_BULK_MEASURE_OUTPUT must name an explicit sanitized JSON artifact path.");

        var phase = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_PHASE")?.Trim().ToLowerInvariant() ?? "representative";
        if (phase is not ("representative" or "matrix" or "admission" or "artwork" or "plans" or "eligibility"))
            throw new InvalidOperationException("MEDIARANKER_BULK_MEASURE_PHASE must be representative, matrix, admission, artwork, or plans.");

        var profile = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_PROFILE")?.Trim();
        if (string.IsNullOrWhiteSpace(profile)) profile = "baseline";

        var samples = ReadPositiveInt("MEDIARANKER_BULK_MEASURE_SAMPLES", 5, maximum: 5);
        var repetitions = ReadPositiveInt("MEDIARANKER_BULK_MEASURE_ENDPOINT_REPETITIONS", 20, maximum: 1000);
        var staging = ReadList("MEDIARANKER_BULK_MEASURE_STAGING", phase is "matrix" or "admission" ? [0, 5_000, 50_000] : [0, 5_000]);
        var eligible = ReadList("MEDIARANKER_BULK_MEASURE_ELIGIBLE", [0, 1, 100]);
        var states = (Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_STATES") ?? "baseline,unchanged,future,updated,backlog")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (states.Length == 0 || states.Any(x => x is not ("baseline" or "unchanged" or "future" or "updated" or "backlog")))
            throw new InvalidOperationException("Unknown staging fixture state.");

        if (staging.Any(x => x is < 0 or > 50_000))
            throw new InvalidOperationException("Staging fixtures must be between 0 and 50000 rows for the recorded local resource envelope.");
        if (eligible.Any(x => x is < 0 or > 100))
            throw new InvalidOperationException("Eligibility percentages must be between 0 and 100.");

        return new(output, phase, profile, samples, repetitions, staging, eligible, states);
    }

    private static int ReadPositiveInt(string name, int fallback, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value is < 1)
            throw new InvalidOperationException($"{name} must be a positive integer.");
        return Math.Min(value, maximum);
    }

    private static IReadOnlyList<int> ReadList(string name, IReadOnlyList<int> fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"{name} contains a non-integer value."))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        return values.Length == 0 ? fallback : values;
    }
}

internal sealed record BulkCatalogMeasurementArtifact(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Profile,
    string Phase,
    string Command,
    string Commit,
    string Runtime,
    BulkCatalogResourceEnvelope ResourceEnvelope,
    IReadOnlyList<BulkCatalogMeasurementRecord> Measurements,
    IReadOnlyList<string> CoverageGaps);

internal sealed record BulkCatalogResourceEnvelope(
    string PostgreSqlImage,
    string TestDatabaseIsolation,
    string HttpIsolation,
    string MaxCommandDuration,
    string MaxProcessRss,
    string MaxTemporaryDisk,
    int WarmupRunsPerScenario,
    int MeasuredRunsPerScenario,
    string LatencyTargets);

internal sealed record BulkCatalogMeasurementRecord(
    string Scenario,
    string Temperature,
    int StagingRows,
    int EligiblePercentage,
    int CanonicalArtworkTargets,
    int ReviewRows,
    int Repetitions,
    long ElapsedMilliseconds,
    long? EndpointP50Milliseconds,
    long? EndpointP95Milliseconds,
    int? SqlCommands,
    int? SqlFailures,
    int? TransactionsStarted,
    long? MaxTransactionMilliseconds,
    long? LeaseHeadroomMilliseconds,
    long? WorkingSetBytes,
    long? AllocatedBytes,
    long? TemporaryDiskBytes,
    int? HttpAttempts,
    int? HttpFailures,
    IReadOnlyList<BulkCatalogHttpAttempt> HttpTimeline,
    IReadOnlyList<BulkCatalogPlan> QueryPlans,
    IReadOnlyList<string> Notes);

internal sealed record BulkCatalogHttpAttempt(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Method,
    string Path,
    int StatusCode,
    bool Succeeded);

internal sealed record BulkCatalogPlan(string Name, bool Available, string PlanHash, string? PlanText);

/// <summary>Captures provider attempts without permitting a network route.</summary>
internal sealed class BulkCatalogFakeProviderHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<BulkCatalogHttpAttempt> attempts = new();
    private int activeRequests;
    private int maxConcurrency;

    public IReadOnlyList<BulkCatalogHttpAttempt> Attempts => attempts.ToArray();
    public int MaxConcurrency => maxConcurrency;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        Interlocked.Increment(ref activeRequests);
        UpdateMaxConcurrency();
        try
        {
            await Task.Yield();
            var path = request.RequestUri?.AbsolutePath ?? "/";
            var response = path.EndsWith("/oauth2/token", StringComparison.OrdinalIgnoreCase)
                ? Json(HttpStatusCode.OK, "{\"access_token\":\"fixture-token\",\"expires_in\":3600}")
                : path.EndsWith("/game_types", StringComparison.OrdinalIgnoreCase)
                    ? Json(HttpStatusCode.OK, "[{\"id\":0,\"type\":\"Main game\"},{\"id\":1,\"type\":\"DLC\"},{\"id\":8,\"type\":\"Remake\"}]")
                    : path.EndsWith("/games", StringComparison.OrdinalIgnoreCase)
                        ? Json(HttpStatusCode.OK, "[{\"id\":101,\"name\":\"Fixture game\",\"game_type\":0,\"first_release_date\":1700000000,\"updated_at\":1700000000,\"cover\":{\"image_id\":\"fixture-cover\"}}]")
                        : Json(HttpStatusCode.NotFound, "{}");
            var finished = DateTimeOffset.UtcNow;
            attempts.Enqueue(new(started, finished, request.Method.Method, path, (int)response.StatusCode, response.IsSuccessStatusCode));
            return response;
        }
        catch
        {
            var finished = DateTimeOffset.UtcNow;
            attempts.Enqueue(new(started, finished, request.Method.Method, request.RequestUri?.AbsolutePath ?? "/", 599, false));
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref activeRequests);
        }
    }

    private void UpdateMaxConcurrency()
    {
        var current = Volatile.Read(ref activeRequests);
        while (current > Volatile.Read(ref maxConcurrency)
               && Interlocked.CompareExchange(ref maxConcurrency, current, Volatile.Read(ref maxConcurrency)) != Volatile.Read(ref maxConcurrency))
        {
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };
}

internal sealed class BulkCatalogHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

/// <summary>Stores only safe command metadata: no SQL text, parameters, or provider rows.</summary>
internal sealed class BulkCatalogDbInstrumentation : DbCommandInterceptor
{
    private readonly ConcurrentQueue<BulkCatalogCommandRecord> commands = new();
    public BulkCatalogTransactionInstrumentation TransactionInstrumentation { get; } = new();

    public IReadOnlyList<BulkCatalogCommandRecord> Commands => commands.ToArray();
    public int TransactionsStarted => TransactionInstrumentation.TransactionsStarted;
    public long MaximumTransactionDurationMilliseconds => TransactionInstrumentation.MaximumDurationMilliseconds;

    public void Reset()
    {
        while (commands.TryDequeue(out _)) { }
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Add(command, eventData.Duration, succeeded: true);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Add(command, eventData.Duration, succeeded: true);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Add(command, eventData.Duration, succeeded: true);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Add(command, eventData.Duration, succeeded: true);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Add(command, eventData.Duration, succeeded: true);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Add(command, eventData.Duration, succeeded: true);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Add(command, eventData.Duration, succeeded: false);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Add(command, eventData.Duration, succeeded: false);
        return Task.CompletedTask;
    }

    private void Add(DbCommand command, TimeSpan duration, bool succeeded)
    {
        var text = command.CommandText.TrimStart();
        var kind = text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || text.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
            ? "reader"
            : text.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) || text.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ? "write" : "other";
        commands.Enqueue(new BulkCatalogCommandRecord(
            kind,
            succeeded,
            Math.Max(0, (long)duration.TotalMilliseconds),
            command.Parameters.Count,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeSql(text))))[..16]));
    }

    private static string NormalizeSql(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();
}

internal sealed record BulkCatalogCommandRecord(string Kind, bool Succeeded, long DurationMilliseconds, int ParameterCount, string StatementHash);

internal sealed class BulkCatalogTransactionInstrumentation : DbTransactionInterceptor
{
    private readonly ConcurrentDictionary<DbTransaction, long> starts = new();
    private long maximumDurationMilliseconds;
    private int started;

    public int TransactionsStarted => Volatile.Read(ref started);
    public long MaximumDurationMilliseconds => Volatile.Read(ref maximumDurationMilliseconds);

    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Interlocked.Increment(ref started);
        starts[result] = Stopwatch.GetTimestamp();
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        TransactionStarted(connection, eventData, result);
        return ValueTask.FromResult(result);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Complete(transaction);
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(transaction);
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Complete(transaction);
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(transaction);
        return Task.CompletedTask;
    }

    private void Complete(DbTransaction transaction)
    {
        if (!starts.TryRemove(transaction, out var start)) return;
        var elapsed = (long)(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        long current;
        do
        {
            current = Volatile.Read(ref maximumDurationMilliseconds);
            if (elapsed <= current) return;
        } while (Interlocked.CompareExchange(ref maximumDurationMilliseconds, elapsed, current) != current);
    }
}

internal sealed class BulkCatalogMeasurementRunner
{
    private static readonly HashSet<string> SupportedGameTypes = new(StringComparer.OrdinalIgnoreCase) { "Main game", "Remake", "Remaster" };
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(15);

    private readonly string connectionString;
    private readonly HttpClient endpointClient;
    private readonly BulkCatalogMeasurementOptions options;
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private readonly List<string> coverageGaps =
    [
        "The harness uses fixture PostgreSQL and a local fake HTTP handler; it does not measure live provider failure incidence or authorize a live run.",
        "Endpoint SQL command counts are not attributed through the existing WebApplicationFactory; direct service/provider runs carry command instrumentation, while endpoint rows carry latency only.",
        "The current plan probes are deliberately labeled illustrative: exact generated eligibility/cleanup/claim SQL plans require an application-level plan capture or a follow-up fixture helper.",
        "Temporary-disk high-water is unavailable in this test-only harness because no owned feed-download directory is created; the artifact records null for that metric.",
        "IMDb full-feed download, gzip integrity, parser interruption, cleanup/load fault injection, and catalog-sized readiness remain separate gates.",
        "Five samples provide comparable local evidence but are not a statistically strong tail estimate."
    ];

    public BulkCatalogMeasurementRunner(string connectionString, HttpClient endpointClient, BulkCatalogMeasurementOptions options)
    {
        this.connectionString = connectionString;
        this.endpointClient = endpointClient;
        this.options = options;
    }

    public async Task<BulkCatalogMeasurementArtifact> RunAsync(CancellationToken cancellationToken)
    {
        using var sessionDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sessionDeadline.CancelAfter(SessionTimeout);
        var measurements = new List<BulkCatalogMeasurementRecord>();
        try
        {
            if (options.Phase == "eligibility")
            {
                measurements.Add(await MeasureUpdatedEligibilityPlansAsync(sessionDeadline.Token));
                await PersistPartialAsync(measurements);
            }
            if (options.Phase is "representative" or "matrix" or "admission")
            {
                foreach (var stagingSize in options.StagingSizes)
                foreach (var eligible in options.EligiblePercentages)
                {
                    foreach (var state in options.States)
                    {
                        if (options.Phase == "representative" && stagingSize > 0 && (eligible != 1 || state is not "baseline"))
                            continue;
                        measurements.AddRange(await MeasureAdmissionScenarioAsync(stagingSize, eligible, state, sessionDeadline.Token));
                        await PersistPartialAsync(measurements);
                    }
                }
            }

            if (options.Phase is "representative" or "matrix" or "artwork")
            {
                measurements.AddRange(await MeasureArtworkScenariosAsync(sessionDeadline.Token));
                await PersistPartialAsync(measurements);
            }

            if (options.Phase is "representative" or "matrix")
            {
                measurements.Add(await MeasureMixedTrafficAsync(sessionDeadline.Token));
                await PersistPartialAsync(measurements);
            }

            if (options.Phase is "plans" or "matrix")
            {
                measurements.Add(await MeasurePlansAsync(sessionDeadline.Token));
                await PersistPartialAsync(measurements);
            }

            return BuildArtifact(measurements);
        }
        finally
        {
            // Preserve all completed rows when a 15-minute session deadline or a
            // fixture fault interrupts the matrix.
            await PersistPartialAsync(measurements);
        }
    }

    private BulkCatalogMeasurementArtifact BuildArtifact(IReadOnlyList<BulkCatalogMeasurementRecord> measurements) => new(
        startedAt,
        DateTimeOffset.UtcNow,
        options.Profile,
        options.Phase,
        Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_COMMAND") ?? "dotnet test --filter BulkCatalogMeasurementTests",
        Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_COMMIT") ?? "unknown",
        $"{Environment.Version}; {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
        new("postgres:16-alpine", "Testcontainers PostgreSQL only; original app/database is never opened", "local fake HTTP handler only; no external URI", "15 seconds per command", "2 GiB", "1 GiB", 1, options.Samples, "p95 <= 1s for 100 targets; p95 <= 3s for 1000 reviews; overlap <= 2x idle"),
        measurements,
        coverageGaps);

    private async Task PersistPartialAsync(IReadOnlyList<BulkCatalogMeasurementRecord> measurements)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(options.OutputPath, JsonSerializer.Serialize(BuildArtifact(measurements), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), CancellationToken.None);
    }

    private async Task<IReadOnlyList<BulkCatalogMeasurementRecord>> MeasureAdmissionScenarioAsync(int stagingRows, int eligiblePercentage, string state, CancellationToken ct)
    {
        var output = new List<BulkCatalogMeasurementRecord>();
        await SeedScenarioAsync(stagingRows, eligiblePercentage, state, artworkTargets: 0, reviewRows: 0, ct);
        await MeasureAdmissionOnceAsync(stagingRows, eligiblePercentage, state, "warmup", ct);
        for (var sample = 0; sample < options.Samples; sample++)
        {
            await SeedScenarioAsync(stagingRows, eligiblePercentage, state, artworkTargets: 0, reviewRows: 0, ct);
            output.Add(await MeasureAdmissionOnceAsync(stagingRows, eligiblePercentage, state, "cold", ct));

            // A second invocation on the same seeded database explicitly captures unchanged replay/admission-only behavior.
            output.Add(await MeasureAdmissionOnceAsync(stagingRows, eligiblePercentage, state, "warm", ct));
        }
        return output;
    }

    private async Task<BulkCatalogMeasurementRecord> MeasureAdmissionOnceAsync(int stagingRows, int eligiblePercentage, string state, string temperature, CancellationToken ct)
    {
        var fake = new BulkCatalogFakeProviderHandler();
        using var http = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/v4/") };
        using var twitch = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/") };
        var client = CreateIgdbClient(http, twitch);
        var instrumentation = new BulkCatalogDbInstrumentation();
        await using var db = CreateDb(instrumentation);
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();
        var leaseHeadroom = (long?)null;
        var loaded = 0;
        var units = 0;
        var maxUnitMilliseconds = 0L;
        try
        {
            if (options.Profile == "candidate")
            {
                using var session = new ImportBudgetSession(new(100, 100000, TimeSpan.FromMinutes(15)));
                while (true)
                {
                    var unitTimer = Stopwatch.StartNew();
                    using var unit = session.CreateUnit(new(5, 20, 500, 1, TimeSpan.FromSeconds(60)));
                    using var unitDeadline = unit.CreateLinkedTokenSource(ct);
                    await using var unitDb = CreateDb(instrumentation);
                    var unitProvider = new IgdbImportSqlProvider(unitDb, NullLogger<IgdbImportSqlProvider>.Instance);
                    var unitLease = await unitProvider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), unitDeadline.Token);
                    unitLease.Should().NotBeNull();
                    IgdbAdmissionResult progress;
                    try
                    {
                        progress = await unitProvider.LoadEligibleGamesAsync(unitLease!, SupportedGameTypes,
                            DateTimeOffset.UtcNow, TimeSpan.FromDays(30), TimeSpan.FromDays(7), unit, unitDeadline.Token);
                        progress.Blocked.Should().BeFalse();
                        loaded += progress.LoadedRows;
                        var leaseState = await unitDb.Set<IgdbImportState>().AsNoTracking().SingleAsync(unitDeadline.Token);
                        var headroom = (long)(leaseState.ClaimedUntil!.Value - DateTimeOffset.UtcNow).TotalMilliseconds;
                        leaseHeadroom = Math.Min(leaseHeadroom ?? long.MaxValue, headroom);
                    }
                    finally
                    {
                        using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await unitProvider.ReleaseLeaseAsync(unitLease!, release.Token);
                    }
                    units++;
                    maxUnitMilliseconds = Math.Max(maxUnitMilliseconds, unitTimer.ElapsedMilliseconds);
                    if (progress.Completed) break;
                    unit.StopReason.Should().Be(ImportStopReason.UnitAdmissionLimit);
                }
            }
            else
            {
            var now = DateTimeOffset.UtcNow;
            var lease = await new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance)
                .TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), ct);
            lease.Should().NotBeNull();
            var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
            loaded = await provider.LoadEligibleGamesAsync(lease!, SupportedGameTypes, now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), ct);
            var persisted = await db.Set<IgdbImportState>().AsNoTracking().SingleAsync(ct);
            leaseHeadroom = persisted.ClaimedUntil is { } until ? (long)(until - DateTimeOffset.UtcNow).TotalMilliseconds : null;
            await provider.ReleaseLeaseAsync(lease!, ct);
            }
        }
        catch (Exception exception)
        {
            coverageGaps.Add($"Admission scenario {stagingRows}/{eligiblePercentage}/{state} recorded failure {exception.GetType().Name}; target behavior was not changed by the harness.");
        }
        timer.Stop();
        var after = CaptureResources();
        return BuildRecord(
            $"igdb-admission-{state}", temperature, stagingRows, eligiblePercentage, 0, 0, timer.Elapsed, instrumentation, fake, before, after,
            leaseHeadroom, endpointP50: null, endpointP95: null,
            notes: [$"eligible rows admitted: {loaded}", $"staging distribution: {stagingRows} rows, {eligiblePercentage}% currently eligible",
                $"bounded candidate units: {units}; maximum unit milliseconds: {maxUnitMilliseconds}",
                "Candidate drains 500-row units with fresh contexts/leases under one cumulative allowance; baseline uses the checkpoint admission entry point. Neither includes an inter-unit scheduler yield."]);
    }

    private async Task<IReadOnlyList<BulkCatalogMeasurementRecord>> MeasureArtworkScenariosAsync(CancellationToken ct)
    {
        var output = new List<BulkCatalogMeasurementRecord>();
        foreach (var targets in new[] { 1, 25, 100 })
        foreach (var state in new[] { "cold", "fresh", "stale", "shared-tv" })
        {
            await SeedScenarioAsync(0, 0, state, targets, state == "shared-tv" ? targets : 0, ct);
            await MeasureArtworkOnceAsync(targets, "warmup", ct);
            for (var sample = 0; sample < options.Samples; sample++)
            {
                await SeedScenarioAsync(0, 0, state, targets, state == "shared-tv" ? targets : 0, ct);
                output.Add(await MeasureArtworkOnceAsync(targets, state, ct));
            }
        }

        await SeedScenarioAsync(0, 0, "review-endpoint", 100, 1_000, ct);
        await WarmupReviewEndpointAsync(ct);
        output.Add(await MeasureReviewEndpointAsync(1_000, 1_000, "cold", ct));
        output.Add(await MeasureReviewEndpointAsync(1_000, 1_000, "warm", ct));
        return output;
    }

    private async Task<BulkCatalogMeasurementRecord> MeasureArtworkOnceAsync(int targets, string state, CancellationToken ct)
    {
        var instrumentation = new BulkCatalogDbInstrumentation();
        await using var db = CreateDb(instrumentation);
        var fake = new BulkCatalogFakeProviderHandler();
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();
        try
        {
            var artwork = new ArtworkService(
                db,
                Options.Create(new IgdbOptions { ArtworkEnabled = false, RequestsPerSecond = 1, MaxConcurrentRequests = 1 }),
                Options.Create(new TmdbOptions { Enabled = true, ReadAccessToken = "fixture-token" }),
                Options.Create(new ArtworkOptions { BatchSize = 100 }),
                TimeProvider.System,
                NullLogger<ArtworkService>.Instance);
            var ids = await db.Media.AsNoTracking().OrderBy(x => x.Id).Take(targets).Select(x => x.Id).ToListAsync(ct);
            await artwork.GetMediaArtworkAsync(ids, ct);
        }
        catch (Exception exception)
        {
            coverageGaps.Add($"Artwork scenario {targets}/{state} recorded failure {exception.GetType().Name}; target behavior was not changed by the harness.");
        }
        timer.Stop();
        var after = CaptureResources();
        return BuildRecord($"artwork-{state}", state, 0, 0, targets, 0, timer.Elapsed, instrumentation, fake, before, after,
            null, null, null, [$"canonical target count: {targets}", "provider HTTP is disabled for this demand-registration measurement"]);
    }

    private async Task<BulkCatalogMeasurementRecord> MeasureReviewEndpointAsync(int targets, int reviews, string temperature, CancellationToken ct)
    {
        var timerValues = new List<long>(options.EndpointRepetitions);
        var failures = 0;
        for (var i = 0; i < options.EndpointRepetitions; i++)
        {
            ct.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            using var response = await endpointClient.GetAsync("/api/Reviews/byMediaType/-3", ct);
            timer.Stop();
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) failures++;
            else
            {
                using var document = JsonDocument.Parse(body);
                document.RootElement.GetArrayLength().Should().Be(reviews, "measurement must preserve every selected review");
            }
            timerValues.Add(timer.ElapsedMilliseconds);
        }

        return new(
            "review-endpoint",
            temperature,
            0,
            0,
            targets,
            reviews,
            timerValues.Count,
            timerValues.Count == 0 ? 0 : timerValues.Max(),
            Percentile(timerValues, .50),
            Percentile(timerValues, .95),
            null,
            null,
            null,
            null,
            null,
            Process.GetCurrentProcess().WorkingSet64,
            GC.GetTotalAllocatedBytes(precise: false),
            null,
            null,
            null,
            [],
            [],
            [$"endpoint failures or membership misses: {failures}", "Endpoint SQL command count is intentionally un-attributed; see coverageGaps."]);
    }

    private async Task WarmupReviewEndpointAsync(CancellationToken ct)
    {
        for (var i = 0; i < options.EndpointRepetitions; i++)
        {
            using var response = await endpointClient.GetAsync("/api/Reviews/byMediaType/-3", ct);
            _ = await response.Content.ReadAsStringAsync(ct);
        }
    }

    private async Task<BulkCatalogMeasurementRecord> MeasureMixedTrafficAsync(CancellationToken ct)
    {
        await SeedScenarioAsync(5_000, 1, "mixed", 25, 25, ct);
        var instrumentation = new BulkCatalogDbInstrumentation();
        await using var db = CreateDb(instrumentation);
        var fake = new BulkCatalogFakeProviderHandler();
        using var http = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/v4/") };
        using var twitch = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/") };
        var client = CreateIgdbClient(http, twitch);
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();
        try
        {
            Task[] tasks =
            {
                client.GetGamesAsync(new IgdbGameQuery(0, 1_000, null, null, 100), ct),
                client.GetCoverAsync("101", ct),
                client.GetGameTypesAsync(ct)
            };
            await Task.WhenAll(tasks);
        }
        catch (Exception exception)
        {
            coverageGaps.Add($"Mixed importer/artwork fake traffic recorded failure {exception.GetType().Name}.");
        }
        timer.Stop();
        var after = CaptureResources();
        return BuildRecord("mixed-importer-artwork", "cold", 5_000, 1, 25, 25, timer.Elapsed, instrumentation, fake, before, after,
            null, null, null, [$"fake HTTP max concurrency: {fake.MaxConcurrency}", "All calls use a shared IgdbRequestLimiter instance."]);
    }

    private async Task<BulkCatalogMeasurementRecord> MeasurePlansAsync(CancellationToken ct)
    {
        var instrumentation = new BulkCatalogDbInstrumentation();
        await using var db = CreateDb(instrumentation);
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();
        var plans = new List<BulkCatalogPlan>();
        foreach (var (name, sql) in PlanQueries)
        {
            try
            {
                var text = await ExecutePlanAsync(db, sql, ct);
                plans.Add(new(name, true, Hash(text), text));
            }
            catch (Exception exception)
            {
                plans.Add(new(name, false, string.Empty, null));
                coverageGaps.Add($"Query plan {name} unavailable: {exception.GetType().Name}.");
            }
        }
        timer.Stop();
        var after = CaptureResources();
        return BuildRecord("query-plans", "cold", 0, 0, 0, 0, timer.Elapsed, instrumentation, new BulkCatalogFakeProviderHandler(), before, after,
            null, null, null, ["Plans are generated only on disposable fixture data."] , plans);
    }

    private static async Task<string> ExecutePlanAsync(PostgreSQLContext db, string sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {sql}";
        command.CommandTimeout = (int)CommandTimeout.TotalSeconds;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var lines = new List<string>();
        while (await reader.ReadAsync(ct)) lines.Add(reader.GetString(0));
        return string.Join(Environment.NewLine, lines);
    }

    private static readonly IReadOnlyList<(string Name, string Sql)> PlanQueries =
    [
        ("illustrative-igdb-eligibility", "SELECT i.igdb_game_id FROM igdb_imports i WHERE i.igdb_game_id > 0 AND i.name IS NOT NULL AND i.name <> '' AND i.first_release_date <= now() AND i.game_type_name IN ('Main game', 'Remake', 'Remaster') AND NOT EXISTS (SELECT 1 FROM media m WHERE m.external_source = 'Igdb' AND m.external_id = i.igdb_game_id::text) ORDER BY i.igdb_game_id LIMIT 500"),
        ("illustrative-imdb-heavy-parent", "SELECT e.parent_tconst, e.season_number, count(*) FROM imdb_import_episodes e INNER JOIN imdb_imports i ON i.tconst = e.tconst GROUP BY e.parent_tconst, e.season_number ORDER BY count(*) DESC LIMIT 100"),
        ("illustrative-imdb-cleanup", "SELECT count(*) FROM imdb_import_ratings WHERE updated_at < now()"),
        ("illustrative-artwork-claim", "SELECT id FROM media_covers WHERE requested_at IS NOT NULL AND next_attempt_at <= now() AND (claimed_until IS NULL OR claimed_until <= now()) ORDER BY next_attempt_at, id LIMIT 1")
    ];

    private async Task SeedScenarioAsync(int stagingRows, int eligiblePercentage, string state, int artworkTargets, int reviewRows, CancellationToken ct)
    {
        var instrumentation = new BulkCatalogDbInstrumentation();
        await using var db = CreateDb(instrumentation);
        await ClearMutableCatalogAsync(db, ct);
        var now = DateTimeOffset.UtcNow;

        if (stagingRows > 0)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            for (var offset = 0; offset < stagingRows; offset += 500)
            {
                var count = Math.Min(500, stagingRows - offset);
                var sql = new StringBuilder("INSERT INTO igdb_imports (igdb_game_id, name, first_release_date, game_type_name, cover_image_id, provider_updated_at, fetched_at, created_at, updated_at) VALUES ");
                await using var command = new NpgsqlCommand { Connection = connection, CommandTimeout = (int)CommandTimeout.TotalSeconds };
                for (var i = 0; i < count; i++)
                {
                    if (i != 0) sql.Append(',');
                    var id = offset + i + 1L;
                    var eligible = ((id - 1) % 100) < eligiblePercentage;
                    var release = eligible ? now.AddDays(-1) : now.AddDays(1);
                    if (state == "future") release = now.AddDays(1);
                    sql.Append($"(@id{i}, @name{i}, @release{i}, @type{i}, @cover{i}, @provider{i}, @fetched{i}, @fetched{i}, @fetched{i})");
                    command.Parameters.AddWithValue($"id{i}", id);
                    command.Parameters.AddWithValue($"name{i}", $"Fixture game {id}");
                    command.Parameters.AddWithValue($"release{i}", release);
                    command.Parameters.AddWithValue($"type{i}", "Main game");
                    command.Parameters.AddWithValue($"cover{i}", $"cover-{id}");
                    command.Parameters.AddWithValue($"provider{i}", now.AddHours(-1));
                    command.Parameters.AddWithValue($"fetched{i}", now.AddHours(-1));
                }
                sql.Append(" ON CONFLICT (igdb_game_id) DO UPDATE SET name = EXCLUDED.name, first_release_date = EXCLUDED.first_release_date");
                command.CommandText = sql.ToString();
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        if (state is "unchanged" or "updated")
        {
            // Seed an already admitted snapshot independently of the implementation being measured.
            // Bound fixture setup too: a whole-set join can time out before admission is measured.
            // ID ranges preserve the same rows and values without changing database statistics explicitly.
            for (var offset = 0; offset < stagingRows; offset += 500)
            {
                var upper = Math.Min(offset + 500, stagingRows);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO media_covers (provider, lookup_kind, lookup_id, outcome, provider_item_id, image_path,
                    checked_at, expires_at, next_attempt_at, attempt_count, version, created_at, updated_at)
                SELECT 'Igdb', 'IgdbGame', igdb_game_id::text, 'Ready', igdb_game_id::text, cover_image_id,
                    fetched_at, fetched_at + interval '30 days', fetched_at + interval '30 days', 0, 1, fetched_at, fetched_at
                FROM igdb_imports WHERE igdb_game_id > {offset} AND igdb_game_id <= {upper}
                    AND first_release_date < date_trunc('day', now() AT TIME ZONE 'UTC') + interval '1 day';
                """, ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO media (title, release_date, external_id, external_source, media_type_id, cover_id, created_at, updated_at)
                SELECT i.name, (i.first_release_date AT TIME ZONE 'UTC')::date, i.igdb_game_id::text, 'Igdb', -1, c.id, i.fetched_at, i.fetched_at
                FROM igdb_imports i JOIN media_covers c ON c.provider = 'Igdb' AND c.lookup_kind = 'IgdbGame' AND c.lookup_id = i.igdb_game_id::text
                WHERE i.igdb_game_id > {offset} AND i.igdb_game_id <= {upper};
                """, ct);
            }
            if (state == "updated")
                await db.Database.ExecuteSqlRawAsync("""
                    UPDATE igdb_imports SET name = name || ' revised', fetched_at = fetched_at + interval '1 hour',
                        provider_updated_at = provider_updated_at + interval '1 hour';
                    """, ct);
        }

        if (state == "backlog" && stagingRows > 0)
        {
            var gameTypeId = -1L;
            var backlog = Math.Min(500, stagingRows);
            db.Media.AddRange(Enumerable.Range(1, backlog).Select(id => new MediaEntity
            {
                Title = $"Existing game {id}", ExternalId = id.ToString(CultureInfo.InvariantCulture), ExternalSource = MediaExternalSource.Igdb,
                MediaTypeId = gameTypeId, ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
            }));
            await db.SaveChangesAsync(ct);
        }

        if (artworkTargets > 0 || reviewRows > 0)
            await SeedArtworkAndReviewsAsync(db, artworkTargets, reviewRows, state, ct);
    }

    private static async Task ClearMutableCatalogAsync(PostgreSQLContext db, CancellationToken ct)
    {
        // Keep system-seeded media types/templates. The fixture is disposable and Respawn
        // also cleans it after the test, but explicit cleanup makes each measured sample repeatable.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM review_fields; DELETE FROM reviews; DELETE FROM media; DELETE FROM media_collections; DELETE FROM media_covers; DELETE FROM igdb_imports;", ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE igdb_import_state SET last_committed_id = 0, run_maximum_id = NULL, run_updated_after = NULL, run_updated_before = NULL, claimed_until = NULL, claim_token = NULL, bootstrap_completed = FALSE, version = version + 1 WHERE id = 1;", ct);
    }

    private static async Task SeedArtworkAndReviewsAsync(PostgreSQLContext db, int targets, int reviews, string state, CancellationToken ct)
    {
        var media = new List<MediaEntity>();
        MediaCollection? series = null;
        if (state == "shared-tv")
        {
            series = new MediaCollection
            {
                Title = "Shared TV fixture",
                CollectionType = MediaCollectionType.Series,
                ExternalId = "tt9999000",
                ExternalSource = MediaExternalSource.Imdb,
                MediaTypeId = -4,
                ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100))
            };
            db.MediaCollections.Add(series);
            await db.SaveChangesAsync(ct);
        }

        for (var i = 0; i < Math.Max(targets, reviews); i++)
        {
            media.Add(new MediaEntity
            {
                Title = $"Artwork fixture {i + 1}",
                ExternalId = state == "shared-tv" ? $"tt{2000000 + i + 1}" : $"tt{1000000 + i + 1}",
                ExternalSource = MediaExternalSource.Imdb,
                MediaTypeId = state == "shared-tv" ? -4 : -3,
                MediaCollection = series,
                ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
            });
        }
        db.Media.AddRange(media);

        if (state is "fresh" or "stale")
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var item in media.Take(targets))
            {
                item.Cover = new MediaCover
                {
                    Provider = ArtworkProvider.Tmdb,
                    LookupKind = state == "shared-tv" ? CoverLookupKind.SeriesImdb : CoverLookupKind.MovieImdb,
                    LookupId = state == "shared-tv" ? "tt9999000" : item.ExternalId!,
                    Outcome = CoverOutcome.Ready,
                    ImagePath = "/fixture.jpg",
                    ProviderItemId = "fixture",
                    CheckedAt = now.AddDays(-1),
                    ExpiresAt = state == "fresh" ? now.AddDays(30) : now.AddMinutes(-1),
                    Version = 1
                };
            }
        }
        await db.SaveChangesAsync(ct);

        if (reviews > 0)
        {
            db.Reviews.AddRange(media.Take(reviews).Select((item, index) => new MediaRankerServer.Modules.Reviews.Data.Entities.Review
            {
                UserId = "test-user-1", MediaId = item.Id, TemplateId = -1, OverallScore = (short)(index % 10 + 1), ReviewTitle = $"Review {index + 1}"
            }));
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<BulkCatalogMeasurementRecord> MeasureUpdatedEligibilityPlansAsync(CancellationToken ct)
    {
        var rows = options.StagingSizes.Single();
        await SeedScenarioAsync(rows, 100, "updated", 0, 0, ct);
        var instrumentation = new BulkCatalogDbInstrumentation();
        var capture = new BulkCatalogActualSqlCapture();
        await using var db = CreateDb(instrumentation, capture);
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), ct);
        lease.Should().NotBeNull();
        using var session = new ImportBudgetSession(new(10, 500, TimeSpan.FromSeconds(60)));
        using var unit = session.CreateUnit(new(1, 10, 500, 1, TimeSpan.FromSeconds(60)));
        var progress = await provider.LoadEligibleGamesAsync(lease!, SupportedGameTypes, DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), unit, ct);
        await provider.ReleaseLeaseAsync(lease!, ct);
        timer.Stop();
        var after = CaptureResources();
        var queries = capture.Commands.Where(command => command.Text.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && command.Text.Contains("igdb_imports", StringComparison.OrdinalIgnoreCase)).ToArray();
        var plans = new List<BulkCatalogPlan>();
        foreach (var phase in new[] { "before-analyze", "after-analyze" })
        {
            if (phase == "after-analyze")
                await db.Database.ExecuteSqlRawAsync("ANALYZE igdb_imports; ANALYZE media; ANALYZE media_covers;", ct);
            var capturedPlans = await BulkCatalogMeasurementValidationTests.ExplainActualCommandsAsync(
                connectionString, $"updated-{rows}-{phase}", queries, ct);
            plans.AddRange(capturedPlans.Select(plan => new BulkCatalogPlan(plan.Name, plan.Available,
                plan.StatementHash, plan.PlanText)));
        }
        return BuildRecord("updated-eligibility-diagnostic", "cold", rows, 100, 0, 0, timer.Elapsed,
            instrumentation, new BulkCatalogFakeProviderHandler(), before, after, null, null, null,
            [$"admitted: {progress.LoadedRows}; blocked: {progress.Blocked}",
                "Diagnostic only: plans replay emitted SELECTs after one 500-row unit, before and after explicit fixture ANALYZE. Not included in throughput comparisons.",
                .. queries.Select(query => $"{query.StatementHash}: actual command {query.DurationMilliseconds}ms; success={query.Succeeded}")], plans);
    }

    private PostgreSQLContext CreateDb(BulkCatalogDbInstrumentation instrumentation, params IInterceptor[] extraInterceptors) => new(new DbContextOptionsBuilder<PostgreSQLContext>()
        .UseNpgsql(connectionString, options => options.CommandTimeout((int)CommandTimeout.TotalSeconds))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(instrumentation, instrumentation.TransactionInstrumentation)
        .AddInterceptors(extraInterceptors)
        .Options);

    private static IgdbClient CreateIgdbClient(HttpClient apiClient, HttpClient twitchClient)
    {
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            RequestsPerSecond = 1,
            MaxConcurrentRequests = 1,
            TimeoutSeconds = 10
        };
        return new IgdbClient(apiClient, new BulkCatalogHttpClientFactory(twitchClient), new IgdbRequestLimiter(options), Options.Create(options));
    }

    private static BulkCatalogMeasurementRecord BuildRecord(
        string scenario, string temperature, int stagingRows, int eligiblePercentage, int artworkTargets, int reviewRows,
        TimeSpan elapsed, BulkCatalogDbInstrumentation instrumentation, BulkCatalogFakeProviderHandler fake,
        ResourceSnapshot before, ResourceSnapshot after, long? leaseHeadroom, long? endpointP50, long? endpointP95,
        IReadOnlyList<string> notes, IReadOnlyList<BulkCatalogPlan>? plans = null)
    {
        var commands = instrumentation.Commands;
        return new(
            scenario,
            temperature,
            stagingRows,
            eligiblePercentage,
            artworkTargets,
            reviewRows,
            1,
            (long)elapsed.TotalMilliseconds,
            endpointP50,
            endpointP95,
            commands.Count,
            commands.Count(command => !command.Succeeded),
            instrumentation.TransactionsStarted,
            instrumentation.MaximumTransactionDurationMilliseconds,
            leaseHeadroom,
            after.WorkingSetBytes,
            Math.Max(0, after.AllocatedBytes - before.AllocatedBytes),
            after.TemporaryDiskBytes is { } afterDisk && before.TemporaryDiskBytes is { } beforeDisk
                ? Math.Max(0, afterDisk - beforeDisk)
                : null,
            fake.Attempts.Count,
            fake.Attempts.Count(attempt => !attempt.Succeeded),
            fake.Attempts,
            plans ?? [],
            [.. notes, .. commands.GroupBy(command => command.StatementHash)
                .OrderByDescending(group => group.Sum(command => command.DurationMilliseconds)).Take(5)
                .Select(group => $"SQL {group.Key}: count={group.Count()}; totalMs={group.Sum(command => command.DurationMilliseconds)}; maxMs={group.Max(command => command.DurationMilliseconds)}")]);
    }

    private static ResourceSnapshot CaptureResources()
    {
        using var process = Process.GetCurrentProcess();
        const long maxProcessRssBytes = 2L * 1024 * 1024 * 1024;
        if (process.PeakWorkingSet64 > maxProcessRssBytes)
            throw new InvalidOperationException($"Bulk measurement process RSS exceeded the 2 GiB resource envelope ({process.PeakWorkingSet64} bytes).");
        return new(process.WorkingSet64, GC.GetTotalAllocatedBytes(precise: false), null);
    }

    private static long? Percentile(IReadOnlyList<long> values, double percentile)
    {
        if (values.Count == 0) return null;
        var ordered = values.OrderBy(value => value).ToArray();
        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private sealed record ResourceSnapshot(long WorkingSetBytes, long AllocatedBytes, long? TemporaryDiskBytes);
}
