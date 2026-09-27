using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

/// <summary>
/// Opt-in matched mixed-load evidence for the importer and the read paths that
/// share its fixture database. The importer uses the real SQL provider and
/// client, while every upstream request is routed to the local fake handler.
/// </summary>
public sealed class BulkCatalogMixedWorkloadTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const int StagingRows = 5_000;
    private const int EligiblePercentage = 1;
    private const int ArtworkTargets = 100;
    private const int ReviewRows = 1_000;
    private const int MaxSqlSeconds = 15;
    private const int MaxSamples = 5;
    private const int MaxEndpointRepetitions = 50;
    private static readonly TimeSpan WholeRunTimeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task OptInMixedWorkload_RecordsIdleAndOverlapEvidence()
    {
        if (!Enabled())
            return;

        var outputPath = RequiredOutputPath();
        var repetitions = ReadPositiveInt("MEDIARANKER_BULK_MIXED_ENDPOINT_REPETITIONS", 20, MaxEndpointRepetitions);
        const int samples = MaxSamples;
        var measurements = new List<BulkCatalogMixedWorkloadSample>(samples * 2);
        var plans = new List<BulkCatalogMixedWorkloadPlan>();
        var coverageGaps = new List<string>
        {
            "The fixture uses PostgreSQL from Testcontainers and local fake provider HTTP only; no live provider or original database is opened.",
            "Endpoint command counts are not attributed through WebApplicationFactory; importer and direct artwork contexts carry command instrumentation, while endpoint latency and membership are recorded.",
            "Temporary-disk high-water is unavailable because this workload does not create an owned download directory.",
            "Five idle and five overlap samples are comparable local evidence, not a production tail or speedup claim.",
#if BOOTSTRAP_CHECKPOINT_BASELINE
            "The checkpoint baseline exposes only legacy import result fields; HTTP sends are counted at the fake handler and cursor fields are null because completion resets the cursor.",
            "The checkpoint baseline plan probe intercepts its first emitted eligibility SELECT before admission writes, then executes that exact SELECT with EXPLAIN on the 50,000-row fixture.",
            "For baseline samples, ImportAdmissionRows is normalized from the legacy EligibleGamesLoaded result; durable cursor fields are null because completion resets the cursor and the result does not expose it.",
#else
            "The generated eligibility EXPLAIN is opt-in and uses one 50,000-row disposable fixture; no plan is captured unless MEDIARANKER_BULK_MIXED_EXPLAIN=1.",
#endif
            "Transaction counts and maximum transaction duration are not instrumented by these contexts; zero values must not be read as proof that no transactions ran.",
            "Artwork provider lookup is disabled in this fixture, so overlap covers artwork SQL reads rather than concurrent artwork provider sends.",
            "Lease headroom is the minimum observed after provider operations; it does not sample the minimum inside an in-flight statement."
        };

        using var timeout = new CancellationTokenSource(WholeRunTimeout);
        try
        {
            if (string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MIXED_PLAN_ONLY"), "1", StringComparison.Ordinal))
            {
                plans.AddRange(await CaptureEligibilityPlanAsync(timeout.Token));
                return;
            }
            // One unrecorded pass warms EF metadata, PostgreSQL plans, the app
            // host, and the fake HTTP client before either comparison arm.
            await SeedMixedFixtureAsync(timeout.Token);
            _ = await RunIdleSampleAsync(repetitions, timeout.Token);
            await SeedMixedFixtureAsync(timeout.Token);
            _ = await RunOverlapSampleAsync(repetitions, timeout.Token);

            for (var sample = 0; sample < samples; sample++)
            {
                await SeedMixedFixtureAsync(timeout.Token);
                measurements.Add(await RunIdleSampleAsync(repetitions, timeout.Token));
                await PersistAsync(outputPath, measurements, plans, coverageGaps);
            }

            for (var sample = 0; sample < samples; sample++)
            {
                await SeedMixedFixtureAsync(timeout.Token);
                measurements.Add(await RunOverlapSampleAsync(repetitions, timeout.Token));
                await PersistAsync(outputPath, measurements, plans, coverageGaps);
            }

            if (string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MIXED_EXPLAIN"), "1", StringComparison.Ordinal))
                plans.AddRange(await CaptureEligibilityPlanAsync(timeout.Token));

            AssertWorkloadGates(measurements);
        }
        finally
        {
            // Preserve completed samples if a command timeout, resource envelope,
            // or fixture failure interrupts the bounded 15-minute run.
            await PersistAsync(outputPath, measurements, plans, coverageGaps);
        }
    }

    private async Task<BulkCatalogMixedWorkloadSample> RunIdleSampleAsync(int endpointRepetitions, CancellationToken ct)
    {
        var importerInstrumentation = new BulkCatalogDbInstrumentation();
        var artworkInstrumentation = new BulkCatalogDbInstrumentation();
        await using var importerDb = CreateDb(importerInstrumentation);
        await using var artworkDb = CreateDb(artworkInstrumentation);
        var fake = new BulkCatalogFakeProviderHandler();
        using var api = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/v4/") };
        using var twitch = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/") };
        var client = CreateIgdbClient(api, twitch);
        var expectedReviewMediaIds = await ExpectedReviewMediaIdsAsync(ct);
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();
        var result = await RunImporterAsync(importerDb, client, ct);
        var artwork = await RunArtworkAsync(artworkDb, ct);
        var review = await MeasureReviewEndpointAsync(endpointRepetitions, expectedReviewMediaIds, ct);
        timer.Stop();
        var after = CaptureResources();

        await AssertImporterProgressAsync(result, fake, expectedStagedRows: StagingRows, ct);
        artwork.Covers.Keys.Should().HaveCount(ArtworkTargets);
        review.Failures.Should().Be(0);
        review.MediaIds.Should().BeEquivalentTo(expectedReviewMediaIds);
        return BuildSample("idle", timer.Elapsed, result, importerInstrumentation, fake, before, after,
            artwork: artwork, review: review, artworkCommands: artworkInstrumentation.Commands.Count,
            endpointRepetitions: endpointRepetitions,
            notes:
            [
                "Importer, both 100-target artwork reads, and review endpoint requests run sequentially in the comparable idle arm.",
                $"The two artwork reads took {artwork.Elapsed.TotalMilliseconds:F0} ms combined; review membership: {review.MediaIds.Count}/{ReviewRows}."
            ],
            artworkInstrumentation: artworkInstrumentation);
    }

    private async Task<BulkCatalogMixedWorkloadSample> RunOverlapSampleAsync(int endpointRepetitions, CancellationToken ct)
    {
        var importerInstrumentation = new BulkCatalogDbInstrumentation();
        var artworkInstrumentation = new BulkCatalogDbInstrumentation();
        var overlap = new MixedWorkloadOverlapInterceptor();
        await using var importerDb = CreateDb(importerInstrumentation, overlap);
        await using var artworkDb = CreateDb(artworkInstrumentation, overlap);

        var fake = new BulkCatalogFakeProviderHandler();
        using var api = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/v4/") };
        using var twitch = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/") };
        var client = CreateIgdbClient(api, twitch);
        var expectedReviewMediaIds = await ExpectedReviewMediaIdsAsync(ct);
        var before = CaptureResources();
        var timer = Stopwatch.StartNew();

        var importerTask = RunImporterAsync(importerDb, client, ct);
        await overlap.ImporterEligibilityEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        // The importer is paused at its eligibility SQL interceptor. The
        // artwork query must enter its own database command before the importer
        // is released, proving overlap at the SQL boundary rather than merely
        // starting three fake HTTP tasks together.
        var artworkTask = RunArtworkAsync(artworkDb, ct);
        var reviewStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reviewTask = MeasureReviewEndpointAsync(endpointRepetitions, expectedReviewMediaIds, ct, reviewStarted);
        try
        {
            await Task.WhenAll(overlap.ArtworkQueryEntered.Task, reviewStarted.Task)
                .WaitAsync(TimeSpan.FromSeconds(5), ct);
            overlap.ReleaseImporter();
            await Task.WhenAll(importerTask, artworkTask, reviewTask);
        }
        finally
        {
            // Do not strand the importer inside the barrier if a bounded wait
            // or an endpoint/artwork operation fails.
            overlap.ReleaseImporter();
        }
        timer.Stop();
        var after = CaptureResources();
        var result = importerTask.Result;
        var artwork = artworkTask.Result;
        var review = reviewTask.Result;

        await AssertImporterProgressAsync(result, fake, expectedStagedRows: StagingRows, ct);
        artwork.Covers.Keys.Should().HaveCount(ArtworkTargets);
        artwork.Covers.Keys.Should().OnlyContain(id => id > 0);
        review.Failures.Should().Be(0);
        review.MediaIds.Should().BeEquivalentTo(expectedReviewMediaIds);
        review.MediaIds.Should().OnlyHaveUniqueItems();

        return BuildSample("overlap", timer.Elapsed, result, importerInstrumentation, fake, before, after,
            artwork: artwork, review: review, artworkCommands: artworkInstrumentation.Commands.Count,
            endpointRepetitions: endpointRepetitions,
            notes:
            [
                "Importer eligibility SQL was held until the direct 100-target artwork SQL and review endpoint request had started.",
                $"The two artwork reads took {artwork.Elapsed.TotalMilliseconds:F0} ms combined.",
                $"Artwork result membership: {artwork.Covers.Count}/{ArtworkTargets}; review membership: {review.MediaIds.Count}/{ReviewRows}."
            ],
            artworkInstrumentation: artworkInstrumentation);
    }

    private async Task<ImporterRun> RunImporterAsync(PostgreSQLContext db, IgdbClient client, CancellationToken ct)
    {
#if BOOTSTRAP_CHECKPOINT_BASELINE
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            PageSize = 100,
            PageBudget = 2,
            LeaseSeconds = 120
        };
#else
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            PageSize = 100,
            LeaseSeconds = 120,
            WorkUnitPageLimit = 2,
            WorkUnitHttpAttemptLimit = 20,
            WorkUnitAdmissionRowLimit = 500,
            WorkUnitAdmissionBatchLimit = 2,
            WorkUnitSeconds = 15,
            ScheduledSessionHttpAttemptLimit = 20,
            ScheduledSessionAdmissionRowLimit = 500,
            ScheduledSessionMinutes = 1
        };
#endif
        var provider = new HeadroomProvider(
            new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance));
        var service = new IgdbImportService(
            client,
            provider,
            Options.Create(options),
            Options.Create(new ArtworkOptions { PositiveCacheDays = 30, NegativeCacheDays = 7 }),
            NullLogger<IgdbImportService>.Instance);
#if BOOTSTRAP_CHECKPOINT_BASELINE
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var linkedDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var timer = Stopwatch.StartNew();
        var legacyResult = await service.ImportAsync(linkedDeadline.Token);
        timer.Stop();
        return new(legacyResult.PagesCommitted, legacyResult.RowsCommitted, legacyResult.EligibleGamesLoaded,
            legacyResult.RunCompleted, legacyResult.LeaseAcquired, timer.Elapsed, legacyResult.EligibleGamesLoaded,
            null, null, provider.MinimumLeaseHeadroomMilliseconds,
            provider.MaximumRenewalIntervalMilliseconds, provider.LeaseDurationMilliseconds);
#else
        using var session = new ImportBudgetSession(new ImportSessionLimits(20, 500, TimeSpan.FromSeconds(15)));
        using var unit = session.CreateUnit(new ImportWorkUnitLimits(2, 20, 500, 2, TimeSpan.FromSeconds(15)));
        var result = await service.ImportAsync(unit, bootstrap: true, ct);
        return new(result.PagesCommitted, result.RowsCommitted, result.EligibleGamesLoaded, result.RunCompleted,
            result.LeaseAcquired, result.Elapsed, result.AdmissionRows, result.DurableCursor, result.RunMaximumId,
            provider.MinimumLeaseHeadroomMilliseconds, provider.MaximumRenewalIntervalMilliseconds,
            provider.LeaseDurationMilliseconds);
#endif
    }

    private async Task<ArtworkMeasurement> RunArtworkAsync(
        PostgreSQLContext db, CancellationToken ct)
    {
        var ids = await db.Media.AsNoTracking().Where(x => x.MediaType == "Movie").OrderBy(x => x.Id)
            .Take(ArtworkTargets).Select(x => x.Id).ToListAsync(ct);
        var service = new ArtworkService(
            db,
            Options.Create(new IgdbOptions { ArtworkEnabled = false, ClientId = "fixture-client", ClientSecret = "fixture-secret" }),
            Options.Create(new TmdbOptions { Enabled = true, ReadAccessToken = "fixture-token" }),
            Options.Create(new ArtworkOptions { BatchSize = ArtworkTargets, LeaseSeconds = 60 }),
            TimeProvider.System,
            NullLogger<ArtworkService>.Instance);

        var timer = Stopwatch.StartNew();
        var firstTimer = Stopwatch.StartNew();
        var first = await service.GetMediaArtworkAsync(ids, ct);
        firstTimer.Stop();
        first.Keys.Should().BeEquivalentTo(ids);
        first.Values.Should().OnlyContain(x =>
            x.Status == "pending" || x.Status == "disabled" || x.Status == "failed");
        // A second read exercises the canonical pending rows created by the
        // first request and verifies that all target keys remain present.
        var secondTimer = Stopwatch.StartNew();
        var second = await service.GetMediaArtworkAsync(ids, ct);
        secondTimer.Stop();
        timer.Stop();
        second.Keys.Should().BeEquivalentTo(ids);
        return new ArtworkMeasurement(
            new Dictionary<long, MediaRankerServer.Modules.Media.Contracts.CoverPresentation>(second),
            timer.Elapsed,
            [firstTimer.ElapsedMilliseconds, secondTimer.ElapsedMilliseconds]);
    }

    private async Task<ReviewEndpointMeasurement> MeasureReviewEndpointAsync(int repetitions,
        IReadOnlySet<long> expectedMediaIds, CancellationToken ct,
        TaskCompletionSource<bool>? firstRequestStarted = null)
    {
        var elapsed = new List<long>(repetitions);
        var mediaIds = new HashSet<long>();
        var failures = 0;
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            var timer = Stopwatch.StartNew();
            if (repetition == 0) firstRequestStarted?.TrySetResult(true);
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(MaxSqlSeconds));
            using var response = await Client.GetAsync("/api/Reviews/byMediaType/Movie", requestTimeout.Token);
            var body = await response.Content.ReadAsStringAsync(requestTimeout.Token);
            timer.Stop();
            elapsed.Add(timer.ElapsedMilliseconds);
            if (!response.IsSuccessStatusCode)
            {
                failures++;
                continue;
            }

            using var document = JsonDocument.Parse(body);
            var rows = document.RootElement.EnumerateArray().ToArray();
            if (rows.Length != ReviewRows)
            {
                failures++;
                continue;
            }

            var ids = rows.Select(row => row.GetProperty("mediaId").GetInt64()).ToArray();
            if (ids.Distinct().Count() != ReviewRows || !ids.ToHashSet().SetEquals(expectedMediaIds))
            {
                failures++;
                continue;
            }

            if (repetition == repetitions - 1)
                foreach (var id in ids) mediaIds.Add(id);
        }

        return new(
            elapsed.Count == 0 ? 0 : elapsed.Max(),
            Percentile(elapsed, 0.50),
            Percentile(elapsed, 0.95),
            failures,
            mediaIds);
    }

    private async Task SeedMixedFixtureAsync(CancellationToken ct)
    {
        await using var db = CreateDb(new BulkCatalogDbInstrumentation());
        await db.Database.ExecuteSqlRawAsync("DELETE FROM review_fields; DELETE FROM reviews; DELETE FROM media; DELETE FROM media_collections; DELETE FROM media_covers; DELETE FROM igdb_imports;", ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM igdb_import_state;", ct);

        var now = DateTimeOffset.UtcNow;
        await using (var connection = new NpgsqlConnection(db.Database.GetConnectionString()))
        {
            await connection.OpenAsync(ct);
            for (var offset = 0; offset < StagingRows; offset += 500)
            {
                var count = Math.Min(500, StagingRows - offset);
                var sql = new StringBuilder("INSERT INTO igdb_imports (igdb_game_id, name, first_release_date, game_type_name, cover_image_id, provider_updated_at, fetched_at, created_at, updated_at) VALUES ");
                await using var command = new NpgsqlCommand { Connection = connection, CommandTimeout = MaxSqlSeconds };
                for (var i = 0; i < count; i++)
                {
                    if (i > 0) sql.Append(',');
                    var id = offset + i + 1L;
                    var eligible = ((id - 1) % 100) < EligiblePercentage;
                    var release = eligible ? now.AddDays(-1) : now.AddDays(1);
                    sql.Append($"(@id{i}, @name{i}, @release{i}, @type{i}, @cover{i}, @provider{i}, @fetched{i}, @fetched{i}, @fetched{i})");
                    command.Parameters.AddWithValue($"id{i}", id);
                    command.Parameters.AddWithValue($"name{i}", $"Mixed fixture game {id}");
                    command.Parameters.AddWithValue($"release{i}", release);
                    command.Parameters.AddWithValue($"type{i}", "Main game");
                    command.Parameters.AddWithValue($"cover{i}", $"mixed-cover-{id}");
                    command.Parameters.AddWithValue($"provider{i}", now.AddHours(-1));
                    command.Parameters.AddWithValue($"fetched{i}", now.AddHours(-1));
                }

                command.CommandText = sql.ToString();
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        var media = Enumerable.Range(0, ReviewRows).Select(index => new MediaEntity
        {
            Title = $"Mixed review movie {index + 1}",
            ExternalId = $"mixed-imdb-{index + 1}",
            ExternalSource = MediaExternalSource.Imdb,
            MediaType = "Movie",
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        }).ToArray();
        db.Media.AddRange(media);
        await db.SaveChangesAsync(ct);
        db.Reviews.AddRange(media.Select((item, index) => new MediaRankerServer.Modules.Reviews.Data.Entities.Review
        {
            UserId = "test-user-1",
            MediaId = item.Id,
            TemplateId = -1,
            OverallScore = (short)(index % 10 + 1),
            ReviewTitle = $"Mixed review {index + 1}"
        }));
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<BulkCatalogMixedWorkloadPlan>> CaptureEligibilityPlanAsync(CancellationToken ct)
    {
        const int explainRows = 50_000;
        await using var seedDb = CreateDb(new BulkCatalogDbInstrumentation());
        await dbClearForExplainAsync(seedDb, ct);
        await SeedStagingOnlyAsync(seedDb.Database.GetConnectionString()!, explainRows, ct);
        var capture = new ActualEligibilitySqlCapture(
#if BOOTSTRAP_CHECKPOINT_BASELINE
            stopAfterCapture: true
#else
            stopAfterCapture: false
#endif
        );
        var instrumentation = new BulkCatalogDbInstrumentation();
        await using var db = CreateDb(instrumentation, capture);
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), ct);
        if (lease is null)
            return [new("actual-eligibility", false, string.Empty, null, "fixture lease was busy")];

#if BOOTSTRAP_CHECKPOINT_BASELINE
        try
        {
            await provider.LoadEligibleGamesAsync(lease, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Main game" },
                DateTimeOffset.UtcNow, TimeSpan.FromDays(30), TimeSpan.FromDays(7), ct);
        }
        catch (EligibilityCommandCapturedException)
        {
            // Preserve the initial fixture while capturing SQL emitted by the real baseline provider.
        }
        finally
        {
            await provider.ReleaseLeaseAsync(lease, ct);
        }
#else
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 500, TimeSpan.FromSeconds(15)));
        using var unit = session.CreateUnit(new ImportWorkUnitLimits(1, 10, 500, 1, TimeSpan.FromSeconds(15)));
        await provider.LoadEligibleGamesAsync(lease, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Main game" },
            DateTimeOffset.UtcNow, TimeSpan.FromDays(30), TimeSpan.FromDays(7), unit, ct);
        await provider.ReleaseLeaseAsync(lease, ct);
#endif

        var queries = capture.Commands.Where(command => command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(command => command.CommandText).ToArray();
        if (queries.Length == 0)
            return [new("actual-eligibility", false, string.Empty, null, "no emitted eligibility command was intercepted")];

        var plans = new List<BulkCatalogMixedWorkloadPlan>();
        foreach (var query in queries)
        {
            var name = query.CommandText.Contains("count(", StringComparison.OrdinalIgnoreCase)
                ? "actual-eligibility-count" : "actual-eligibility-page";
            try
            {
                var planText = await ExplainAsync(db, query, ct);
                plans.Add(new(name, true, Hash(planText), planText, null));
            }
            catch (Exception exception)
            {
                plans.Add(new(name, false, string.Empty, null, $"EXPLAIN failed: {exception.GetType().Name}"));
            }
        }
        return plans;
    }

    private static async Task<string> ExplainAsync(PostgreSQLContext db, CapturedCommand source, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = MaxSqlSeconds;
        command.CommandText = $"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {source.CommandText.Trim().TrimEnd(';')}";
        foreach (var parameter in source.Parameters)
        {
            var copy = command.CreateParameter();
            copy.ParameterName = parameter.ParameterName;
            copy.Value = parameter.Value ?? DBNull.Value;
            if (parameter.NpgsqlDbType is { } parameterType && copy is NpgsqlParameter npgsqlCopy)
                npgsqlCopy.NpgsqlDbType = parameterType;
            command.Parameters.Add(copy);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        var lines = new List<string>();
        while (await reader.ReadAsync(ct)) lines.Add(reader.GetString(0));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task dbClearForExplainAsync(PostgreSQLContext db, CancellationToken ct) =>
        await db.Database.ExecuteSqlRawAsync("DELETE FROM review_fields; DELETE FROM reviews; DELETE FROM media; DELETE FROM media_collections; DELETE FROM media_covers; DELETE FROM igdb_imports; DELETE FROM igdb_import_state;", ct);

    private async Task SeedStagingOnlyAsync(string connectionString, int rows, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        for (var offset = 0; offset < rows; offset += 500)
        {
            var count = Math.Min(500, rows - offset);
            var sql = new StringBuilder("INSERT INTO igdb_imports (igdb_game_id, name, first_release_date, game_type_name, cover_image_id, provider_updated_at, fetched_at, created_at, updated_at) VALUES ");
            await using var command = new NpgsqlCommand { Connection = connection, CommandTimeout = MaxSqlSeconds };
            for (var i = 0; i < count; i++)
            {
                if (i > 0) sql.Append(',');
                var id = offset + i + 1L;
                sql.Append($"(@id{i}, @name{i}, @release{i}, 'Main game', @cover{i}, @stamp{i}, @stamp{i}, @stamp{i}, @stamp{i})");
                command.Parameters.AddWithValue($"id{i}", id);
                command.Parameters.AddWithValue($"name{i}", $"Plan fixture game {id}");
                command.Parameters.AddWithValue($"release{i}", now.AddDays(-1));
                command.Parameters.AddWithValue($"cover{i}", $"plan-cover-{id}");
                command.Parameters.AddWithValue($"stamp{i}", now.AddHours(-1));
            }
            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private PostgreSQLContext CreateDb(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<PostgreSQLContext>()
        .UseNpgsql(FixtureConnectionString(), options => options.CommandTimeout(MaxSqlSeconds))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(interceptors)
        .Options);

    private string FixtureConnectionString()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        return db.Database.GetDbConnection().ConnectionString;
    }

    private static IgdbClient CreateIgdbClient(HttpClient api, HttpClient twitch)
    {
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            RequestsPerSecond = 4,
            MaxConcurrentRequests = 1,
            TimeoutSeconds = 10
        };
        return new IgdbClient(api, new BulkCatalogHttpClientFactory(twitch), new IgdbRequestLimiter(options), Options.Create(options));
    }

    private async Task AssertImporterProgressAsync(ImporterRun importer, BulkCatalogFakeProviderHandler fake,
        int expectedStagedRows, CancellationToken ct)
    {
        importer.RunCompleted.Should().BeTrue();
        importer.PagesCommitted.Should().Be(1);
        importer.RowsCommitted.Should().Be(1);
        importer.EligibleGamesLoaded.Should().Be(50,
            "the fixture has 50 eligible staged rows and the stale fetched version must preserve their existing values");
        importer.AdmissionRows.Should().Be(50,
            "the fetched provider version is older than staging and both API versions report the 50 eligible games loaded");
        fake.Attempts.Count.Should().BeGreaterThanOrEqualTo(3, "every actual fake HTTP send is counted at the handler boundary");
        importer.MinimumLeaseHeadroomMilliseconds.Should().BeGreaterThan(0);
        importer.MaximumRenewalIntervalMilliseconds.Should().NotBeNull();
        importer.LeaseDurationMilliseconds.Should().BeGreaterThanOrEqualTo(
            importer.MaximumRenewalIntervalMilliseconds!.Value * 2,
            "the configured lease must cover at least twice the longest observed renewal interval");
        await using var db = CreateDb();
        (await db.Set<IgdbImport>().CountAsync(ct)).Should().Be(expectedStagedRows);
        (await db.Set<IgdbImportState>().AsNoTracking().SingleAsync(ct)).BootstrapCompleted.Should().BeTrue();
    }

    private static BulkCatalogMixedWorkloadSample BuildSample(
        string mode,
        TimeSpan elapsed,
        ImporterRun importer,
        BulkCatalogDbInstrumentation importerInstrumentation,
        BulkCatalogFakeProviderHandler fake,
        ResourceSnapshot before,
        ResourceSnapshot after,
        ArtworkMeasurement? artwork,
        ReviewEndpointMeasurement? review,
        int artworkCommands,
        int endpointRepetitions,
        IReadOnlyList<string> notes,
        BulkCatalogDbInstrumentation? artworkInstrumentation = null) => new(
        mode,
        StagingRows,
        EligiblePercentage,
        ArtworkTargets,
        ReviewRows,
        endpointRepetitions,
        (long)elapsed.TotalMilliseconds,
        importer.Elapsed.TotalMilliseconds,
        artwork?.Elapsed.TotalMilliseconds,
        artwork is null ? null : Percentile(artwork.ReadMilliseconds, 0.50),
        artwork is null ? null : Percentile(artwork.ReadMilliseconds, 0.95),
        review?.P50Milliseconds,
        review?.P95Milliseconds,
        review?.Failures ?? 0,
        importer.PagesCommitted,
        importer.RowsCommitted,
        importer.AdmissionRows,
        importer.EligibleGamesLoaded,
        fake.Attempts.Count,
        importerInstrumentation.Commands.Count,
        (artworkInstrumentation?.Commands.Count ?? artworkCommands) + importerInstrumentation.Commands.Count,
        importerInstrumentation.Commands.Count(command => !command.Succeeded)
            + (artworkInstrumentation?.Commands.Count(command => !command.Succeeded) ?? 0),
        importerInstrumentation.TransactionsStarted + (artworkInstrumentation?.TransactionsStarted ?? 0),
        Math.Max(importerInstrumentation.MaximumTransactionDurationMilliseconds,
            artworkInstrumentation?.MaximumTransactionDurationMilliseconds ?? 0),
        importer.DurableCursor,
        importer.RunMaximumId,
        importer.MinimumLeaseHeadroomMilliseconds,
        importer.MaximumRenewalIntervalMilliseconds,
        importer.LeaseDurationMilliseconds,
        after.WorkingSetBytes,
        Math.Max(0, after.AllocatedBytes - before.AllocatedBytes),
        null,
        fake.Attempts.Count,
        fake.Attempts.Count(attempt => !attempt.Succeeded),
        notes);

    private static void AssertWorkloadGates(IReadOnlyList<BulkCatalogMixedWorkloadSample> samples)
    {
        var idle = samples.Where(x => x.Mode == "idle").Select(x => x.WallMilliseconds).OrderBy(x => x).ToArray();
        var overlap = samples.Where(x => x.Mode == "overlap").Select(x => x.WallMilliseconds).OrderBy(x => x).ToArray();
        idle.Should().HaveCount(MaxSamples);
        overlap.Should().HaveCount(MaxSamples);
        var idleP95 = Percentile(idle, 0.95);
        var overlapP95 = Percentile(overlap, 0.95);
        overlapP95.Should().BeLessThanOrEqualTo(idleP95 * 2, "mixed workload must stay within 2x the idle wall-time envelope");

        foreach (var sample in samples.Where(x => x.Mode == "overlap"))
        {
            sample.ArtworkTwoReadElapsedMilliseconds.Should().NotBeNull("both 100-target artwork reads run in the overlap arm");
            sample.ArtworkReadP95Milliseconds.Should().BeLessThanOrEqualTo(1_000);
            sample.ReviewP95Milliseconds.Should().BeLessThanOrEqualTo(3_000);
            sample.ReviewFailures.Should().Be(0);
            sample.SqlFailures.Should().Be(0);
        }
    }

    private static ResourceSnapshot CaptureResources()
    {
        using var process = Process.GetCurrentProcess();
        const long maxRss = 2L * 1024 * 1024 * 1024;
        process.PeakWorkingSet64.Should().BeLessThanOrEqualTo(maxRss);
        return new(process.WorkingSet64, GC.GetTotalAllocatedBytes(precise: false));
    }

    private async Task<IReadOnlySet<long>> ExpectedReviewMediaIdsAsync(CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        return (await db.Reviews.AsNoTracking().Where(x => x.UserId == "test-user-1")
            .Select(x => x.MediaId).ToListAsync(ct)).ToHashSet();
    }

    private async Task PersistAsync(string outputPath, IReadOnlyList<BulkCatalogMixedWorkloadSample> samples,
        IReadOnlyList<BulkCatalogMixedWorkloadPlan> plans, IReadOnlyList<string> gaps)
    {
        var artifact = new BulkCatalogMixedWorkloadArtifact(
            DateTimeOffset.UtcNow,
            Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MEASURE_PROFILE") ?? "baseline",
            "fixture PostgreSQL + local fake provider HTTP only",
            "dotnet test --no-build --filter FullyQualifiedName~BulkCatalogMixedWorkloadTests",
            samples,
            plans,
            gaps);
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), CancellationToken.None);
    }

    private static string RequiredOutputPath()
    {
        var path = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MIXED_OUTPUT");
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("MEDIARANKER_BULK_MIXED_OUTPUT must name an explicit sanitized JSON artifact path.");
        return path;
    }

    private static bool Enabled() => string.Equals(
        Environment.GetEnvironmentVariable("MEDIARANKER_BULK_MIXED"), "1", StringComparison.Ordinal);

    private static int ReadPositiveInt(string name, int fallback, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 1)
            throw new InvalidOperationException($"{name} must be a positive integer.");
        return Math.Min(value, maximum);
    }

    private static long Percentile(IReadOnlyList<long> values, double percentile)
    {
        if (values.Count == 0) return 0;
        var ordered = values.OrderBy(value => value).ToArray();
        var index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private sealed class MixedWorkloadOverlapInterceptor : DbCommandInterceptor
    {
        private int importerSeen;
        private int artworkSeen;
        private readonly TaskCompletionSource<bool> releaseImporter =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ImporterEligibilityEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ArtworkQueryEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseImporter() => releaseImporter.TrySetResult(true);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;
            if (sql.Contains("igdb_imports", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref importerSeen, 1) == 0)
            {
                ImporterEligibilityEntered.TrySetResult(true);
                await releaseImporter.Task.WaitAsync(cancellationToken);
            }
            else if ((sql.Contains("FROM media AS", StringComparison.OrdinalIgnoreCase)
                      || sql.Contains("FROM \"media\" AS", StringComparison.OrdinalIgnoreCase))
                     && Interlocked.Exchange(ref artworkSeen, 1) == 0)
            {
                ArtworkQueryEntered.TrySetResult(true);
            }

            return result;
        }
    }

    private sealed class EligibilityCommandCapturedException : Exception;

    private sealed class ActualEligibilitySqlCapture(bool stopAfterCapture = false) : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> commands = new();
        public IReadOnlyList<CapturedCommand> Commands => commands.ToArray();

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbCommand command)
        {
            if (!command.CommandText.Contains("igdb_imports", StringComparison.OrdinalIgnoreCase)) return;
            commands.Enqueue(new CapturedCommand(command.CommandText,
                command.Parameters.Cast<DbParameter>().Select(parameter => new CapturedParameter(
                    parameter.ParameterName,
                    parameter.Value,
                    (parameter as NpgsqlParameter)?.NpgsqlDbType)).ToArray()));
            if (stopAfterCapture) throw new EligibilityCommandCapturedException();
        }
    }

    private sealed record CapturedCommand(string CommandText, IReadOnlyList<CapturedParameter> Parameters);
    private sealed record CapturedParameter(string ParameterName, object? Value, NpgsqlTypes.NpgsqlDbType? NpgsqlDbType);

    private sealed record ImporterRun(
        int PagesCommitted,
        int RowsCommitted,
        int EligibleGamesLoaded,
        bool RunCompleted,
        bool LeaseAcquired,
        TimeSpan Elapsed,
        int? AdmissionRows,
        long? DurableCursor,
        long? RunMaximumId,
        long? MinimumLeaseHeadroomMilliseconds,
        long? MaximumRenewalIntervalMilliseconds,
        long LeaseDurationMilliseconds);
    private sealed record ArtworkMeasurement(
        IReadOnlyDictionary<long, MediaRankerServer.Modules.Media.Contracts.CoverPresentation> Covers,
        TimeSpan Elapsed,
        IReadOnlyList<long> ReadMilliseconds);
    private sealed record ResourceSnapshot(long WorkingSetBytes, long AllocatedBytes);

    private sealed class HeadroomProvider(IIgdbImportProvider inner) : IIgdbImportProvider
    {
        private long minimumHeadroomMilliseconds = long.MaxValue;
        private long maximumRenewalIntervalMilliseconds;
        private long lastRenewalTimestamp;
        private DateTimeOffset? lastClaimedUntil;
        private long leaseDurationMilliseconds;

        public long? MinimumLeaseHeadroomMilliseconds => Volatile.Read(ref minimumHeadroomMilliseconds) == long.MaxValue
            ? null
            : Volatile.Read(ref minimumHeadroomMilliseconds);
        public long? MaximumRenewalIntervalMilliseconds => Volatile.Read(ref maximumRenewalIntervalMilliseconds) <= 0
            ? null
            : Volatile.Read(ref maximumRenewalIntervalMilliseconds);
        public long LeaseDurationMilliseconds => Volatile.Read(ref leaseDurationMilliseconds);

        public async Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct)
        {
            var lease = await inner.TryAcquireLeaseAsync(now, leaseDuration, ct);
            Observe(lease);
            return lease;
        }

        public async Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? maximumId,
            DateTimeOffset? updatedAfter, DateTimeOffset? updatedBefore, DateTimeOffset now, CancellationToken ct)
        {
            var state = await inner.StartRunAsync(lease, bootstrap, maximumId, updatedAfter, updatedBefore, now, ct);
            Observe(lease);
            return state;
        }

        public async Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page,
            long committedId, CancellationToken ct)
        {
            var state = await inner.CommitPageAsync(lease, page, committedId, ct);
            Observe(lease);
            return state;
        }

        public async Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct)
        {
            await inner.CompleteRunAsync(lease, now, ct);
            Observe(lease);
        }

        public Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct) => inner.ReleaseLeaseAsync(lease, ct);

        public async Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> supportedGameTypes,
            DateTimeOffset now, TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
        {
            var loaded = await inner.LoadEligibleGamesAsync(lease, supportedGameTypes, now,
                positiveCacheDuration, negativeCacheDuration, ct);
            Observe(lease);
            return loaded;
        }

#if !BOOTSTRAP_CHECKPOINT_BASELINE
        public async Task<IgdbAdmissionResult> LoadEligibleGamesAsync(IgdbImportLease lease,
            IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration,
            TimeSpan negativeCacheDuration, ImportWorkUnitBudget budget, CancellationToken ct)
        {
            var result = await inner.LoadEligibleGamesAsync(lease, supportedGameTypes, now,
                positiveCacheDuration, negativeCacheDuration, budget, ct);
            Observe(lease);
            return result;
        }

        public Task<DateTimeOffset?> GetLeaseBusyUntilAsync(CancellationToken ct) => inner.GetLeaseBusyUntilAsync(ct);
#endif

        private void Observe(IgdbImportLease? lease)
        {
            if (lease?.State.ClaimedUntil is not { } until) return;
            var observedAt = Stopwatch.GetTimestamp();
            Volatile.Write(ref leaseDurationMilliseconds, (long)lease.LeaseDuration.TotalMilliseconds);
            if (lastClaimedUntil is { } previousUntil && previousUntil != until)
            {
                var renewalInterval = (long)Stopwatch.GetElapsedTime(lastRenewalTimestamp, observedAt).TotalMilliseconds;
                long previousMaximum;
                do
                {
                    previousMaximum = Volatile.Read(ref maximumRenewalIntervalMilliseconds);
                    if (renewalInterval <= previousMaximum) break;
                }
                while (Interlocked.CompareExchange(ref maximumRenewalIntervalMilliseconds, renewalInterval, previousMaximum) != previousMaximum);
                lastRenewalTimestamp = observedAt;
                lastClaimedUntil = until;
            }
            else if (lastClaimedUntil is null)
            {
                lastRenewalTimestamp = observedAt;
                lastClaimedUntil = until;
            }

            var headroom = (long)(until - DateTimeOffset.UtcNow).TotalMilliseconds;
            long current;
            do
            {
                current = Volatile.Read(ref minimumHeadroomMilliseconds);
                if (headroom >= current) return;
            }
            while (Interlocked.CompareExchange(ref minimumHeadroomMilliseconds, headroom, current) != current);
        }
    }

    private sealed record ReviewEndpointMeasurement(
        long MaxMilliseconds,
        long P50Milliseconds,
        long P95Milliseconds,
        int Failures,
        IReadOnlySet<long> MediaIds);
}

internal sealed record BulkCatalogMixedWorkloadArtifact(
    DateTimeOffset CreatedAt,
    string Profile,
    string Isolation,
    string Command,
    IReadOnlyList<BulkCatalogMixedWorkloadSample> Samples,
    IReadOnlyList<BulkCatalogMixedWorkloadPlan> Plans,
    IReadOnlyList<string> CoverageGaps);

internal sealed record BulkCatalogMixedWorkloadSample(
    string Mode,
    int StagingRows,
    int EligiblePercentage,
    int ArtworkTargets,
    int ReviewRows,
    int EndpointRepetitions,
    long WallMilliseconds,
    double ImportElapsedMilliseconds,
    double? ArtworkTwoReadElapsedMilliseconds,
    long? ArtworkReadP50Milliseconds,
    long? ArtworkReadP95Milliseconds,
    long? ReviewP50Milliseconds,
    long? ReviewP95Milliseconds,
    int ReviewFailures,
    int ImportPagesCommitted,
    int ImportRowsCommitted,
    int? ImportAdmissionRows,
    int ImportEligibleGamesLoaded,
    int ImportHttpAttempts,
    int ImportSqlCommands,
    int SqlCommands,
    int SqlFailures,
    int TransactionsStarted,
    long MaxTransactionMilliseconds,
    long? DurableCursor,
    long? RunMaximumId,
    long? MinimumLeaseHeadroomMilliseconds,
    long? MaximumRenewalIntervalMilliseconds,
    long LeaseDurationMilliseconds,
    long WorkingSetBytes,
    long AllocatedBytes,
    long? TemporaryDiskBytes,
    int HttpAttempts,
    int HttpFailures,
    IReadOnlyList<string> Notes);

internal sealed record BulkCatalogMixedWorkloadPlan(
    string Name,
    bool Available,
    string PlanHash,
    string? PlanText,
    string? Failure);
