using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
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
/// Separate opt-in validation for measurement instrumentation and the actual
/// pre-change IMDb load SQL. It is deliberately separate from the broader
/// harness so a parent can run this before application changes land.
/// </summary>
public sealed class BulkCatalogMeasurementValidationTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task KnownCounters_MatchSmallFixtureExpectations()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_BULK_VALIDATE_COUNTERS"), "1", StringComparison.Ordinal))
            return;

        var outputPath = RequiredOutputPath("MEDIARANKER_BULK_VALIDATE_COUNTERS_OUTPUT");
        var connectionString = FixtureConnectionString();
        var capture = new BulkCatalogActualSqlCapture();
        var counters = new BulkCatalogDbInstrumentation();
        await using var db = new PostgreSQLContext(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(connectionString, settings => settings.CommandTimeout(15)).UseSnakeCaseNamingConvention()
            .AddInterceptors(capture, capture.TransactionCapture, counters, counters.TransactionInstrumentation).Options);

        _ = await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync();
            await transaction.CommitAsync();
        }

        var fake = new BulkCatalogFakeProviderHandler();
        using var api = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/v4/") };
        using var twitch = new HttpClient(fake) { BaseAddress = new Uri("http://fixture.invalid/") };
        var igdbOptions = new IgdbOptions
        {
            ImportEnabled = true,
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            RequestsPerSecond = 4,
            MaxConcurrentRequests = 1,
            TimeoutSeconds = 10
        };
        var client = new IgdbClient(api, new BulkCatalogHttpClientFactory(twitch), new IgdbRequestLimiter(igdbOptions), Options.Create(igdbOptions));
        _ = await client.GetGamesAsync(new IgdbGameQuery(0, 1_000, null, null, 1), CancellationToken.None);

        var actual = new BulkCatalogCounterResult(
            capture.Commands.Count(command => command.Succeeded && command.Kind == "reader"),
            capture.TransactionsStarted,
            fake.Attempts.Count,
            fake.Attempts.Count(attempt => !attempt.Succeeded),
            fake.MaxConcurrency);
        var expected = new BulkCatalogCounterResult(2, 1, 2, 0, 1);
        actual.ReaderCommands.Should().Be(expected.ReaderCommands);
        actual.TransactionsStarted.Should().Be(expected.TransactionsStarted);
        actual.HttpAttempts.Should().Be(expected.HttpAttempts);
        actual.HttpFailures.Should().Be(expected.HttpFailures);
        actual.MaxHttpConcurrency.Should().Be(expected.MaxHttpConcurrency);
        counters.Commands.Should().HaveCount(2);
        counters.Commands.Should().OnlyContain(command => command.Succeeded && command.Kind == "reader");
        counters.TransactionsStarted.Should().Be(1);
        counters.TransactionInstrumentation.CompletedDurationsMilliseconds.Should().ContainSingle()
            .Which.Should().Be(counters.MaximumTransactionDurationMilliseconds);

        await WriteJsonAsync(outputPath, new BulkCatalogCounterArtifact(
            DateTimeOffset.UtcNow,
            "fixture PostgreSQL plus local fake HTTP only",
            expected,
            actual,
            capture.ToArtifacts(),
            fake.Attempts));
    }

    [Fact]
    public async Task ImdbHeavyParent_RecordsActualLoadSqlAndReplaySeasonMinDate()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_BULK_VALIDATE_IMDB"), "1", StringComparison.Ordinal))
            return;

        var outputPath = RequiredOutputPath("MEDIARANKER_BULK_VALIDATE_IMDB_OUTPUT");
        var connectionString = FixtureConnectionString();
        var samples = ReadSamples();
        var profile = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_VALIDATE_PROFILE") ?? "baseline";
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var ct = deadline.Token;
        var startedAt = DateTimeOffset.UtcNow;
        var samplesOut = new List<BulkCatalogImdbSample>();
        var coverageGaps = new List<string>
        {
            "This is an isolated heavy-parent fixture, not a full IMDb download or readiness proof.",
            "The test interceptor caps statements at 15 seconds and the entire fixture at 15 minutes; production baseline timeout configuration is not a readiness guarantee.",
            "Automatic database transactions around individual ExecuteSqlRaw calls are provider-managed and may not surface as DbTransactionInterceptor events; explicit transaction counts are reported separately.",
            "The parent has 1,100 ordinary season groups and one 10,000-episode season group; no linear extrapolation to the full feed is made."
        };

        var loadPlans = new List<BulkCatalogActualPlan>();
        await SeedHeavyParentAsync(connectionString, ct);
        await RunHeavyLoadAsync(connectionString, capture: null, warmup: true, ct);
        for (var sample = 0; sample < samples; sample++)
        {
            await SeedHeavyParentAsync(connectionString, ct);
            var capture = new BulkCatalogActualSqlCapture();
            var measured = await RunHeavyLoadAsync(connectionString, capture, warmup: false, ct);
            samplesOut.Add(measured);
            if (sample == 0)
                loadPlans.AddRange(await ExplainActualCommandsAsync(connectionString, "imdb-load-warm",
                    capture.Commands.Where(command => command.Text.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                        || command.Text.TrimStart().StartsWith("WITH", StringComparison.OrdinalIgnoreCase)), ct));
            await WriteJsonAsync(outputPath, new BulkCatalogImdbArtifact(
                startedAt,
                DateTimeOffset.UtcNow,
                profile,
                "fixture-owned PostgreSQL; fake/no provider HTTP",
                "one warm-up plus five measured runs; each measured run is freshly seeded",
                samplesOut,
                [],
                coverageGaps));
        }

        var actualPlans = loadPlans.Concat(await MeasureActualPlansAsync(connectionString, ct)).ToArray();
        await WriteJsonAsync(outputPath, new BulkCatalogImdbArtifact(
            startedAt,
            DateTimeOffset.UtcNow,
            profile,
            "fixture-owned PostgreSQL; fake/no provider HTTP",
            "one warm-up plus five measured runs; each measured run is freshly seeded",
            samplesOut,
            actualPlans,
            coverageGaps));
    }

    private string FixtureConnectionString()
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PostgreSQLContext>().Database.GetDbConnection().ConnectionString;
    }

    private static PostgreSQLContext CreateDb(string connectionString, BulkCatalogActualSqlCapture capture) => new(new DbContextOptionsBuilder<PostgreSQLContext>()
        .UseNpgsql(connectionString, options => options.CommandTimeout(15))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(capture, capture.TransactionCapture)
        .Options);

    private static int ReadSamples()
    {
        var raw = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_VALIDATE_IMDB_SAMPLES");
        if (string.IsNullOrWhiteSpace(raw)) return 5;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 5)
            throw new InvalidOperationException("MEDIARANKER_BULK_VALIDATE_IMDB_SAMPLES must be between 1 and 5.");
        return value;
    }

    private static string RequiredOutputPath(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{variable} must name an explicit JSON artifact path.");
        return value;
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private async Task<BulkCatalogImdbSample> RunHeavyLoadAsync(string connectionString, BulkCatalogActualSqlCapture? capture, bool warmup, CancellationToken ct)
    {
        if (capture is null)
        {
            await using var warmupDb = CreateDb(connectionString, new BulkCatalogActualSqlCapture());
            var warmupProvider = new ImdbLoadSqlProvider(warmupDb, NullLogger<ImdbLoadSqlProvider>.Instance);
            await warmupProvider.LoadSeriesCollectionsAsync(1_000, ct);
            await warmupProvider.LoadSeasonCollectionsAsync(ct);
            await warmupProvider.LoadEpisodeMediaAsync(ct);
            return new BulkCatalogImdbSample("warmup", 0, 0, 0, 0, 0, 0, [], [], []);
        }

        await using var db = CreateDb(connectionString, capture);
        var provider = new ImdbLoadSqlProvider(db, NullLogger<ImdbLoadSqlProvider>.Instance);
        var timer = Stopwatch.StartNew();
        var series = await provider.LoadSeriesCollectionsAsync(1_000, ct);
        var seasons = await provider.LoadSeasonCollectionsAsync(ct);
        var episodes = await provider.LoadEpisodeMediaAsync(ct);
        seasons.Affected.Should().Be(1_101, "the fixture has 1,100 ordinary season groups and one 10,000-episode season group");
        episodes.Affected.Should().Be(11_100, "the fixture has 1,100 one-episode groups plus one 10,000-episode group");
        var firstSeasonBeforeReplay = await db.MediaCollections.AsNoTracking()
            .Where(collection => collection.ExternalId == "tt-heavy-series" && collection.CollectionType == MediaCollectionType.Season && collection.Title == "9999")
            .OrderBy(collection => collection.Title)
            .Select(collection => collection.ReleaseDate)
            .FirstAsync(ct);
        firstSeasonBeforeReplay.Should().Be(new DateOnly(1980, 7, 1), "the oldest episode is at the end of the oversized input group");
        await provider.LoadSeasonCollectionsAsync(ct);
        var firstSeasonAfterReplay = await db.MediaCollections.AsNoTracking()
            .Where(collection => collection.ExternalId == "tt-heavy-series" && collection.CollectionType == MediaCollectionType.Season && collection.Title == "9999")
            .OrderBy(collection => collection.Title)
            .Select(collection => collection.ReleaseDate)
            .FirstAsync(ct);
        timer.Stop();
        firstSeasonAfterReplay.Should().Be(firstSeasonBeforeReplay, "replaying season aggregation must preserve the existing minimum episode date");

        var commandArtifacts = capture.ToArtifacts();
        return new(
            warmup ? "warmup" : "measured",
            (long)timer.Elapsed.TotalMilliseconds,
            series.Affected,
            seasons.Affected,
            episodes.Affected,
            commandArtifacts.Count,
            capture.TransactionsStarted,
            commandArtifacts,
            [],
            [$"season replay minimum date preserved: {firstSeasonAfterReplay:O}", "The first measured run includes the replay check."]);
    }

    private async Task SeedHeavyParentAsync(string connectionString, CancellationToken ct)
    {
        await using var db = CreateDb(connectionString, new BulkCatalogActualSqlCapture());
        await db.Database.ExecuteSqlRawAsync("DELETE FROM media; DELETE FROM media_collections; DELETE FROM imdb_import_episodes; DELETE FROM imdb_import_ratings; DELETE FROM imdb_imports;", ct);

        var parent = new ImdbImport
        {
            Tconst = "tt-heavy-series", TitleType = "tvSeries", PrimaryTitle = "Heavy fixture series", OriginalTitle = "Heavy fixture series",
            IsAdult = false, StartYear = 1980, RawLine = "heavy-series"
        };
        db.ImdbImports.Add(parent);
        db.ImdbImportRatings.Add(new ImdbImportRating { Tconst = parent.Tconst, AverageRating = 8.0m, NumVotes = 10_000 });

        var basics = new List<ImdbImport>(11_100);
        var episodes = new List<ImdbImportEpisode>(11_100);
        for (var season = 1; season <= 1_100; season++)
        {
            var tconst = $"tt-season-{season:0000}-episode-0001";
            basics.Add(BasicEpisode(tconst, 1900 + (season % 100), season));
            episodes.Add(Episode(tconst, season, 1));
        }
        for (var episode = 1; episode <= 10_000; episode++)
        {
            var tconst = $"tt-heavy-episode-{episode:00000}";
            basics.Add(BasicEpisode(tconst, episode == 10_000 ? 1980 : 1999, 9_999));
            episodes.Add(Episode(tconst, 9_999, episode));
        }
        db.ImdbImports.AddRange(basics);
        db.ImdbImportEpisodes.AddRange(episodes);
        await db.SaveChangesAsync(ct);

        static ImdbImport BasicEpisode(string tconst, int year, int season) => new()
        {
            Tconst = tconst,
            TitleType = "tvEpisode",
            PrimaryTitle = $"Episode {season} fixture",
            OriginalTitle = $"Episode {season} fixture",
            IsAdult = false,
            StartYear = year,
            RawLine = tconst
        };

        static ImdbImportEpisode Episode(string tconst, int season, int episode) => new()
        {
            Tconst = tconst,
            ParentTconst = "tt-heavy-series",
            SeasonNumber = season,
            EpisodeNumber = episode,
            RawLine = $"{tconst}\ttt-heavy-series\t{season}\t{episode}"
        };
    }

    private async Task<IReadOnlyList<BulkCatalogActualPlan>> MeasureActualPlansAsync(string connectionString, CancellationToken ct)
    {
        var plans = new List<BulkCatalogActualPlan>();
        await SeedEligibilityFixtureAsync(connectionString, ct);
        var eligibilityCapture = new BulkCatalogActualSqlCapture();
        await using (var db = CreateDb(connectionString, eligibilityCapture))
        {
            var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
            var lease = await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), ct);
            if (lease is not null)
            {
                await provider.LoadEligibleGamesAsync(lease, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Main game", "Remake", "Remaster" }, DateTimeOffset.UtcNow, TimeSpan.FromDays(30), TimeSpan.FromDays(7), ct);
                await provider.ReleaseLeaseAsync(lease, ct);
            }
        }
        plans.AddRange(await ExplainActualCommandsAsync(connectionString, "igdb-eligibility", eligibilityCapture.Commands.Where(command => command.Text.Contains("igdb_imports", StringComparison.OrdinalIgnoreCase)), ct));

        var cleanupCapture = new BulkCatalogActualSqlCapture();
        await using (var db = CreateDb(connectionString, cleanupCapture))
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await new ImdbImportSqlProvider(db, NullLogger<ImdbImportSqlProvider>.Instance).DeleteStaleRatingsAsync(DateTimeOffset.UtcNow.AddMinutes(1), ct);
            await transaction.RollbackAsync(ct);
        }
        plans.AddRange(await ExplainActualCommandsAsync(connectionString, "imdb-cleanup", cleanupCapture.Commands.Where(command => command.Text.Contains("imdb_import_ratings", StringComparison.OrdinalIgnoreCase)), ct));

        await SeedArtworkClaimFixtureAsync(connectionString, ct);
        var claimCapture = new BulkCatalogActualSqlCapture();
        await using (var db = CreateDb(connectionString, claimCapture))
        {
            var artworkOptions = new IgdbOptions { ArtworkEnabled = true, ClientId = "fixture-client", ClientSecret = "fixture-secret", RequestsPerSecond = 1, MaxConcurrentRequests = 1 };
            var processor = new ArtworkProcessor(
                db,
                new FixtureIgdbClient(),
                new FixtureTmdbClient(),
                Options.Create(artworkOptions),
                Options.Create(new TmdbOptions { Enabled = false }),
                Options.Create(new ArtworkOptions { BatchSize = 1, LeaseSeconds = 60, MaxAttempts = 3, RetrySeconds = 1, MaxRetrySeconds = 10 }),
                TimeProvider.System,
                NullLogger<ArtworkProcessor>.Instance,
                new IgdbRequestLimiter(artworkOptions),
                new TmdbRequestCooldown());
            await processor.RunAsync(ct);
        }
        plans.AddRange(await ExplainActualCommandsAsync(connectionString, "artwork-claim", claimCapture.Commands.Where(command => command.Text.Contains("media_covers", StringComparison.OrdinalIgnoreCase)), ct));

        return plans;
    }

    private static async Task SeedEligibilityFixtureAsync(string connectionString, CancellationToken ct)
    {
        await using var db = CreateDb(connectionString, new BulkCatalogActualSqlCapture());
        await db.Database.ExecuteSqlRawAsync("DELETE FROM media; DELETE FROM media_collections; DELETE FROM media_covers; DELETE FROM igdb_imports; UPDATE igdb_import_state SET claimed_until = NULL, claim_token = NULL WHERE id = 1;", ct);
        db.Set<IgdbImport>().Add(new IgdbImport
        {
            IgdbGameId = 900001, Name = "Eligibility fixture", GameTypeName = "Main game", FirstReleaseDate = DateTimeOffset.UtcNow.AddDays(-1),
            CoverImageId = "eligibility-cover", ProviderUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1), FetchedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedArtworkClaimFixtureAsync(string connectionString, CancellationToken ct)
    {
        await using var db = CreateDb(connectionString, new BulkCatalogActualSqlCapture());
        await db.Database.ExecuteSqlRawAsync("DELETE FROM media; DELETE FROM media_collections; DELETE FROM media_covers;", ct);
        db.Media.Add(new MediaEntity { Title = "Claim fixture", ExternalId = "900001", ExternalSource = MediaExternalSource.Igdb, MediaTypeId = -1 });
        db.MediaCovers.Add(new MediaCover
        {
            Provider = ArtworkProvider.Igdb, LookupKind = CoverLookupKind.IgdbGame, LookupId = "900001", Outcome = CoverOutcome.Pending,
            RequestedAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow, AttemptCount = 0, Version = 0
        });
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<IReadOnlyList<BulkCatalogActualPlan>> ExplainActualCommandsAsync(
        string connectionString, string name, IEnumerable<BulkCatalogActualSqlCommand> commands, CancellationToken ct)
    {
        var plans = new List<BulkCatalogActualPlan>();
        foreach (var command in commands.Where(command => command.Text.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                                                          || command.Text.TrimStart().StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
                                                          || command.Text.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                                                          || command.Text.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                                                          || command.Text.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)).DistinctBy(command => command.StatementHash))
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await using var explain = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {command.Text.Trim().TrimEnd(';')}", connection, transaction);
                explain.CommandTimeout = 15;
                foreach (var parameter in command.Parameters)
                    explain.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Value ?? DBNull.Value) { DbType = parameter.DbType });
                var lines = new List<string>();
                await using (var reader = await explain.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct)) lines.Add(reader.GetString(0));
                }
                await transaction.RollbackAsync(ct);
                plans.Add(new($"{name}:{command.StatementHash}", true, command.StatementHash, string.Join(Environment.NewLine, lines)));
            }
            catch (Exception exception)
            {
                plans.Add(new($"{name}:{command.StatementHash}", false, command.StatementHash, $"unavailable:{exception.GetType().Name}"));
            }
        }
        return plans;
    }

    private sealed class FixtureIgdbClient : IIgdbClient
    {
        public Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct) => Task.FromResult(new ArtworkResult("fixture-item", "fixture-cover"));
        public Task<long> GetMaximumGameIdAsync(CancellationToken ct) => Task.FromResult(1L);
        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<IgdbGameType>>([]);
        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<IgdbGame>>([]);
    }

    private sealed class FixtureTmdbClient : ITmdbClient
    {
        public Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct) => Task.FromResult(new ArtworkResult("fixture-item", "/fixture.jpg"));
    }
}

internal sealed record BulkCatalogCounterResult(int ReaderCommands, int TransactionsStarted, int HttpAttempts, int HttpFailures, int MaxHttpConcurrency);
internal sealed record BulkCatalogCounterArtifact(DateTimeOffset RecordedAt, string Isolation, BulkCatalogCounterResult Expected, BulkCatalogCounterResult Actual, IReadOnlyList<BulkCatalogActualSqlArtifact> Commands, IReadOnlyList<BulkCatalogHttpAttempt> HttpAttempts);
internal sealed record BulkCatalogImdbArtifact(DateTimeOffset StartedAt, DateTimeOffset UpdatedAt, string Profile, string Isolation, string Sampling, IReadOnlyList<BulkCatalogImdbSample> Samples, IReadOnlyList<BulkCatalogActualPlan> Plans, IReadOnlyList<string> CoverageGaps);
internal sealed record BulkCatalogImdbSample(string Kind, long ElapsedMilliseconds, int SeriesAffected, int SeasonsAffected, int EpisodesAffected, int SqlCommandCount, int TransactionsStarted, IReadOnlyList<BulkCatalogActualSqlArtifact> Commands, IReadOnlyList<BulkCatalogActualPlan> Plans, IReadOnlyList<string> Notes);
internal sealed record BulkCatalogActualPlan(string Name, bool Available, string StatementHash, string PlanText);
internal sealed record BulkCatalogActualSqlArtifact(string Kind, bool Succeeded, long DurationMilliseconds, string Text, IReadOnlyList<BulkCatalogActualParameterArtifact> Parameters, string StatementHash);
internal sealed record BulkCatalogActualParameterArtifact(string Name, string DbType, string? Value);

internal sealed class BulkCatalogActualSqlCapture : DbCommandInterceptor
{
    private readonly ConcurrentQueue<BulkCatalogActualSqlCommand> commands = new();
    public BulkCatalogActualTransactionCapture TransactionCapture { get; } = new();
    public IReadOnlyList<BulkCatalogActualSqlCommand> Commands => commands.ToArray();
    public int TransactionsStarted => TransactionCapture.TransactionsStarted;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        Bound(command);
        return ValueTask.FromResult(result);
    }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        Bound(command);
        return ValueTask.FromResult(result);
    }
    private static void Bound(DbCommand command)
    {
        command.CommandTimeout = 15;
        using var process = Process.GetCurrentProcess();
        if (process.PeakWorkingSet64 > 2L * 1024 * 1024 * 1024)
            throw new InvalidOperationException("Measurement exceeded its 2 GiB process memory allowance.");
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) { Add(command, eventData.Duration, true, "reader"); return result; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) { Add(command, eventData.Duration, true, "reader"); return ValueTask.FromResult(result); }
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) { Add(command, eventData.Duration, true, "write"); return result; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default) { Add(command, eventData.Duration, true, "write"); return ValueTask.FromResult(result); }
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result) { Add(command, eventData.Duration, true, "scalar"); return result; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default) { Add(command, eventData.Duration, true, "scalar"); return ValueTask.FromResult(result); }
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Add(command, eventData.Duration, false, "failed");
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default) { Add(command, eventData.Duration, false, "failed"); return Task.CompletedTask; }

    public IReadOnlyList<BulkCatalogActualSqlArtifact> ToArtifacts() => Commands.Select(command => new BulkCatalogActualSqlArtifact(
        command.Kind, command.Succeeded, command.DurationMilliseconds, command.Text,
        command.Parameters.Select(parameter => new BulkCatalogActualParameterArtifact(parameter.Name, parameter.DbType.ToString(), parameter.Value?.ToString())).ToArray(),
        command.StatementHash)).ToArray();

    private void Add(DbCommand command, TimeSpan duration, bool succeeded, string kind)
    {
        var text = command.CommandText;
        commands.Enqueue(new BulkCatalogActualSqlCommand(
            kind,
            succeeded,
            (long)duration.TotalMilliseconds,
            text,
            command.Parameters.Cast<DbParameter>().Select(CloneParameter).ToArray(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16]));
    }

    private static BulkCatalogActualParameter CloneParameter(DbParameter parameter) => new(parameter.ParameterName, parameter.DbType, parameter.Value is DBNull ? null : parameter.Value);
}

internal sealed record BulkCatalogActualParameter(string Name, DbType DbType, object? Value);
internal sealed record BulkCatalogActualSqlCommand(string Kind, bool Succeeded, long DurationMilliseconds, string Text, IReadOnlyList<BulkCatalogActualParameter> Parameters, string StatementHash);

internal sealed class BulkCatalogActualTransactionCapture : DbTransactionInterceptor
{
    private int started;
    public int TransactionsStarted => Volatile.Read(ref started);
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result) { Interlocked.Increment(ref started); return result; }
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default) { Interlocked.Increment(ref started); return ValueTask.FromResult(result); }
}
