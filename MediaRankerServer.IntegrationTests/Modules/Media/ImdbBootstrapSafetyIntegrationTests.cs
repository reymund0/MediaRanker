using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ImdbBootstrapSafetyIntegrationTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task RegisteredImdbProviderDoesNotRetainEfSqlOrRawValuesOnDatabaseFailure()
    {
        using var fixtureScope = Factory.Services.CreateScope();
        var fixtureDb = fixtureScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var capture = new CapturingLogProvider();
        using var services = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Information).AddProvider(capture))
            .AddDbContext<PostgreSQLContext>(options => options
                .UseNpgsql(fixtureDb.Database.GetConnectionString()).UseSnakeCaseNamingConvention())
            .AddMediaModule(new ConfigurationBuilder().Build(), new FixtureEnvironment())
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IImdbImportProvider>();
        var row = new ImdbTsvRow("tt-log-fixture", "movie", "private-title-marker", "private-title-marker",
            false, 2020, null, null, null, "raw-provider-marker");
        var run = () => provider.ImportBasicsAsync([row, row], CancellationToken.None);

        await run.Should().ThrowAsync<Exception>(); // PostgreSQL rejects a duplicate conflict target in one statement.
        capture.Messages.Should().Contain(message => message.Contains("IMDb SQL stage basics failed for 2 rows"));
        capture.Messages.Should().NotContain(message => message.Contains("private-title-marker")
            || message.Contains("raw-provider-marker") || message.Contains("INSERT INTO"));
        capture.Messages.Clear();
        var ordinaryDb = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        await ordinaryDb.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync();
        capture.Messages.Should().Contain(message => message.Contains("SELECT 1"),
            "ordinary context diagnostics must remain enabled");
    }

    private sealed class FixtureEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Integration";
        public string ApplicationName { get; set; } = "MediaRankerServer";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }
        private sealed class CaptureLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task CleanupUsesOneDatabaseCutoffAcrossUnitsAndReplayIsEmpty()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var options = Options.Create(new ImdbImportOptions { MaxCleanupRowsPerUnit = 1 });
        var provider = new ImdbImportSqlProvider(db, NullLogger<ImdbImportSqlProvider>.Instance, options);

        db.ImdbImportRatings.AddRange(
            Rating("tt8100001"), Rating("tt8100002"), Rating("tt8100003"));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE imdb_import_ratings
            SET updated_at = now() - interval '2 days'
            WHERE tconst IN ('tt8100001', 'tt8100002', 'tt8100003');
            """);

        var cutoff = await provider.GetDatabaseUtcNowAsync(CancellationToken.None);
        db.ImdbImportRatings.Add(Rating("tt8100004"));
        await db.SaveChangesAsync();

        var deleted = 0;
        while (true)
        {
            var unit = await provider.DeleteStaleRatingsAsync(cutoff, 1, CancellationToken.None);
            deleted += unit;
            if (unit == 0) break;
        }

        deleted.Should().Be(3);
        (await db.ImdbImportRatings.AsNoTracking().Select(r => r.Tconst).ToListAsync())
            .Should().Equal("tt8100004");
        (await provider.DeleteStaleRatingsAsync(cutoff, 1, CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task BasicsAndRatingsReplayReportAffectedRowsForUpserts()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var provider = new ImdbImportSqlProvider(db, NullLogger<ImdbImportSqlProvider>.Instance,
            Options.Create(new ImdbImportOptions()));
        var basics = new ImdbTsvRow("tt8199901", "movie", "Replay title", "Replay title", false,
            2020, null, 90, "Drama", "tt8199901\\tmovie\\tReplay title");
        var rating = new ImdbRatingTsvRow("tt8199901", 8.2m, 1234, "tt8199901\\t8.2\\t1234");

        var firstBasics = await provider.ImportBasicsAsync([basics], CancellationToken.None);
        var replayedBasics = await provider.ImportBasicsAsync([basics], CancellationToken.None);
        var firstRating = await provider.ImportRatingsAsync([rating], CancellationToken.None);
        var replayedRating = await provider.ImportRatingsAsync([rating], CancellationToken.None);

        firstBasics.Affected.Should().Be(1);
        replayedBasics.Affected.Should().Be(1);
        firstRating.Affected.Should().Be(1);
        replayedRating.Affected.Should().Be(1);
        replayedRating.ToString().Should().Contain("Affected = 1").And.NotContain("Inserted");
        (await db.ImdbImports.CountAsync(row => row.Tconst == "tt8199901")).Should().Be(1);
        (await db.ImdbImportRatings.CountAsync(row => row.Tconst == "tt8199901")).Should().Be(1);
    }

    [Fact]
    public async Task LoadUsesUnitCapAndKeepsAHeavySeasonAsOneGroup()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        await SeedLoadGraphAsync(db);

        var options = Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 1 });
        var provider = new ImdbLoadSqlProvider(db, NullLogger<ImdbLoadSqlProvider>.Instance, options);
        var seasonUnit = await provider.LoadSeasonCollectionsBatchAsync(null, null, 1, CancellationToken.None);
        var seasonEnd = await provider.LoadSeasonCollectionsBatchAsync(
            seasonUnit.NextParentTconst, seasonUnit.NextSeasonNumber, 1, CancellationToken.None);

        seasonUnit.Affected.Should().Be(1);
        seasonUnit.HasMore.Should().BeTrue("the unit is full and must probe the next key");
        seasonEnd.Affected.Should().Be(0);
        seasonEnd.HasMore.Should().BeFalse("the three episodes belong to one season group");

        var service = new ImdbLoadService(provider, options, NullLogger<ImdbLoadService>.Instance);
        var result = await service.LoadAsync();

        result.Affected.Should().Be(6);
        (await db.MediaCollections.AsNoTracking().CountAsync(c => c.CollectionType == MediaCollectionType.Season))
            .Should().Be(1);
        (await db.Media.AsNoTracking().CountAsync(m => m.ExternalSource == MediaExternalSource.Imdb))
            .Should().Be(4);
    }

    [Fact]
    public async Task EpisodeConflictPreservesStagingAndRefreshesDomainRowsAndAdvancesPastIt()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var options = Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 1 });

        var series = new MediaCollection
        {
            Title = "Series",
            CollectionType = MediaCollectionType.Series,
            ExternalId = "tt8200000",
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4
        };
        db.MediaCollections.Add(series);
        await db.SaveChangesAsync();
        var oldSeason = new MediaCollection
        {
            Title = "9",
            CollectionType = MediaCollectionType.Season,
            ParentMediaCollectionId = series.Id,
            ExternalId = series.ExternalId,
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4
        };
        var season = new MediaCollection
        {
            Title = "1",
            CollectionType = MediaCollectionType.Season,
            ParentMediaCollectionId = series.Id,
            ExternalId = series.ExternalId,
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4
        };
        db.MediaCollections.AddRange(oldSeason, season);
        await db.SaveChangesAsync();
        db.ImdbImports.AddRange(
            EpisodeImport("tt8200001", "Episode One"),
            EpisodeImport("tt8200002", "Episode Two"));
        db.ImdbImportEpisodes.Add(new ImdbImportEpisode
        {
            Tconst = "tt8200001",
            ParentTconst = "tt8200000",
            SeasonNumber = 1,
            EpisodeNumber = 1,
            RawLine = "old-staging"
        });
        db.Media.Add(new MediaEntity
        {
            Title = "Preserved Domain Title",
            ExternalId = "tt8200001",
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4,
            MediaCollectionId = oldSeason.Id
        });
        await db.SaveChangesAsync();

        var importProvider = new ImdbImportSqlProvider(db, NullLogger<ImdbImportSqlProvider>.Instance, options);
        var imported = await importProvider.ImportEpisodesAsync(
            [
                new ImdbEpisodeTsvRow("tt8200001", "tt-not-the-original-parent", 7, 9, "new-staging"),
                new ImdbEpisodeTsvRow("tt8200002", "tt8200000", 1, 2, "new-episode")
            ], CancellationToken.None);
        imported.Affected.Should().Be(1);

        var loadProvider = new ImdbLoadSqlProvider(db, NullLogger<ImdbLoadSqlProvider>.Instance, options);
        var first = await loadProvider.LoadEpisodeMediaBatchAsync(null, 1, CancellationToken.None);
        var second = await loadProvider.LoadEpisodeMediaBatchAsync(first.NextKey, 1, CancellationToken.None);

        first.Affected.Should().Be(1);
        first.HasMore.Should().BeTrue();
        second.Affected.Should().Be(1);
        second.HasMore.Should().BeTrue("a full batch requires a terminal keyset probe");
        var terminal = await loadProvider.LoadEpisodeMediaBatchAsync(second.NextKey, 1, CancellationToken.None);
        terminal.Affected.Should().Be(0);
        terminal.HasMore.Should().BeFalse();

        var staging = await db.ImdbImportEpisodes.AsNoTracking().SingleAsync(e => e.Tconst == "tt8200001");
        staging.ParentTconst.Should().Be("tt8200000");
        staging.SeasonNumber.Should().Be(1);
        staging.RawLine.Should().Be("old-staging");
        var domain = await db.Media.AsNoTracking().SingleAsync(m => m.ExternalId == "tt8200001");
        domain.Title.Should().Be("Episode One");
        domain.MediaCollectionId.Should().Be(season.Id);
        (await db.Media.AsNoTracking().SingleAsync(m => m.ExternalId == "tt8200002")).Title.Should().Be("Episode Two");
    }

    [Fact]
    public async Task CanceledLoadStatementLeavesRowsUnchanged()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.ImdbImports.Add(new ImdbImport
        {
            Tconst = "tt8300001",
            TitleType = "movie",
            PrimaryTitle = "Canceled Movie",
            OriginalTitle = "Canceled Movie",
            RawLine = "canceled"
        });
        db.ImdbImportRatings.Add(new ImdbImportRating
        {
            Tconst = "tt8300001",
            AverageRating = 7.0m,
            NumVotes = 10_000
        });
        await db.SaveChangesAsync();

        var provider = new ImdbLoadSqlProvider(db, NullLogger<ImdbLoadSqlProvider>.Instance,
            Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 1 }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var run = () => provider.LoadNonSeriesMediaBatchAsync(1_000, null, 1, cancellation.Token);
        await run.Should().ThrowAsync<OperationCanceledException>();
        (await db.Media.AsNoTracking().CountAsync(m => m.ExternalId == "tt8300001")).Should().Be(0);
    }

    private static ImdbImportRating Rating(string tconst) => new()
    {
        Tconst = tconst,
        AverageRating = 7.0m,
        NumVotes = 10_000
    };

    private static ImdbImport EpisodeImport(string tconst, string title) => new()
    {
        Tconst = tconst,
        TitleType = "tvEpisode",
        PrimaryTitle = title,
        OriginalTitle = title,
        StartYear = 2020,
        RawLine = title
    };

    private static async Task SeedLoadGraphAsync(PostgreSQLContext db)
    {
        db.ImdbImports.AddRange(
            new ImdbImport
            {
                Tconst = "tt8000001", TitleType = "movie", PrimaryTitle = "Movie", OriginalTitle = "Movie",
                StartYear = 2020, RawLine = "movie"
            },
            new ImdbImport
            {
                Tconst = "tt8000002", TitleType = "tvSeries", PrimaryTitle = "Series", OriginalTitle = "Series",
                StartYear = 2020, RawLine = "series"
            },
            EpisodeImport("tt8000003", "Episode 1"),
            EpisodeImport("tt8000004", "Episode 2"),
            EpisodeImport("tt8000005", "Episode 3"));
        db.ImdbImportEpisodes.AddRange(
            new ImdbImportEpisode { Tconst = "tt8000003", ParentTconst = "tt8000002", SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e1" },
            new ImdbImportEpisode { Tconst = "tt8000004", ParentTconst = "tt8000002", SeasonNumber = 1, EpisodeNumber = 2, RawLine = "e2" },
            new ImdbImportEpisode { Tconst = "tt8000005", ParentTconst = "tt8000002", SeasonNumber = 1, EpisodeNumber = 3, RawLine = "e3" });
        db.ImdbImportRatings.AddRange(Rating("tt8000001"), Rating("tt8000002"));
        db.MediaCollections.Add(new MediaCollection
        {
            Title = "Series",
            CollectionType = MediaCollectionType.Series,
            ExternalId = "tt8000002",
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4
        });
        await db.SaveChangesAsync();
    }
}
