using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ImdbLoadIntegrationTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    // Seed rows: inserted via EF directly into imdb_imports table.
    // (a) eligible movie with year
    private const string TconstMovieWithYear   = "tt0000001";
    // (b) eligible movie with null year
    private const string TconstMovieNullYear   = "tt0000002";
    // (c) eligible video game
    private const string TconstVideoGame       = "tt0000003";
    // (d) ineligible tvSeries
    private const string TconstTvSeries        = "tt0000004";

    private async Task SeedImdbImportsAsync(bool withMutatedDuplicate = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var rows = new List<ImdbImport>
        {
            new() { Tconst = TconstMovieWithYear,  TitleType = "movie",    PrimaryTitle = "Original Title",  OriginalTitle = "Original Title",  StartYear = 2001, RawLine = "a" },
            new() { Tconst = TconstMovieNullYear,  TitleType = "movie",    PrimaryTitle = "No Year Movie",   OriginalTitle = "No Year Movie",   StartYear = null, RawLine = "b" },
            new() { Tconst = TconstVideoGame,      TitleType = "videoGame",PrimaryTitle = "Cool Game",       OriginalTitle = "Cool Game",       StartYear = 2005, RawLine = "c" },
            new() { Tconst = TconstTvSeries,       TitleType = "tvSeries", PrimaryTitle = "Some Series",     OriginalTitle = "Some Series",     StartYear = 2010, RawLine = "d" },
        };

        if (withMutatedDuplicate)
        {
            // Remove the original row for (a) and replace with mutated title — simulates a second import run
            // with updated data. Since tconst is unique, we use ExecuteSqlRaw to update in-place.
            await db.Database.ExecuteSqlRawAsync($"""
                UPDATE imdb_imports SET primary_title = 'Updated Title' WHERE tconst = '{TconstMovieWithYear}'
                """);
            return;
        }

        foreach (var row in rows)
        {
            // Avoid duplicate constraint if test is retried
            var exists = await db.ImdbImports.AnyAsync(i => i.Tconst == row.Tconst);
            if (!exists) db.ImdbImports.Add(row);
        }

        await db.SaveChangesAsync();

        // Seed matching ratings rows so the load phase INNER JOIN passes (num_votes >= default MinVotesMovies/Tv=1000, MinVotesVideoGames=50).
        await SeedRatingsAsync(db,
        [
            new() { Tconst = TconstMovieWithYear, AverageRating = 7.5m, NumVotes = 5000 },
            new() { Tconst = TconstMovieNullYear, AverageRating = 6.0m, NumVotes = 5000 },
            new() { Tconst = TconstVideoGame,     AverageRating = 8.0m, NumVotes = 200  },
            new() { Tconst = TconstTvSeries,      AverageRating = 7.0m, NumVotes = 5000 },
        ]);
    }

    private static async Task SeedRatingsAsync(PostgreSQLContext db, IEnumerable<ImdbImportRating> ratings)
    {
        foreach (var r in ratings)
        {
            var exists = await db.ImdbImportRatings.AnyAsync(x => x.Tconst == r.Tconst);
            if (!exists) db.ImdbImportRatings.Add(r);
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LoadAsync_InsertsEligibleRowsOnly()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();

        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var mediaRows = await db.Media.ToListAsync();

        mediaRows.Should().HaveCount(3);
        mediaRows.Should().NotContain(m => m.ExternalId == TconstTvSeries);
    }

    [Fact]
    public async Task LoadAsync_MovieWithYear_HasMidYearReleaseDate()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstMovieWithYear);

        row.Should().NotBeNull();
        row!.ReleaseDate.Should().Be(new DateOnly(2001, 7, 1));
    }

    [Fact]
    public async Task LoadAsync_MovieWithNullYear_HasNullReleaseDate()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstMovieNullYear);

        row.Should().NotBeNull();
        row!.ReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_VideoGame_HasVideoGameMediaType()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstVideoGame);

        row.Should().NotBeNull();
        row!.MediaType.Should().Be("VideoGame");
    }

    [Fact]
    public async Task LoadAsync_SetsExternalSourceToImdb()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var rows = await db.Media.Where(m => m.ExternalSource == MediaExternalSource.Imdb).ToListAsync();

        rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task LoadAsync_IsIdempotent_AndUpdatesTitle()
    {
        await SeedImdbImportsAsync();

        using var scope = Factory.Services.CreateScope();
        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();

        // First run
        await loadService.LoadAsync();

        // Mutate the title of row (a) in imdb_imports
        await SeedImdbImportsAsync(withMutatedDuplicate: true);

        // Second run — should DO UPDATE, not INSERT new row
        var result = await loadService.LoadAsync();

        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var allMedia = await db.Media.ToListAsync();
        allMedia.Should().HaveCount(3, "no new rows should be inserted on second run");

        var updatedRow = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == TconstMovieWithYear);
        updatedRow.Should().NotBeNull();
        updatedRow!.Title.Should().Be("Updated Title");
    }

    // -------------------------------------------------------------------------
    // Series + Season load tests
    // -------------------------------------------------------------------------

    private const string TconstSeries1 = "tt1000001";

    private async Task<PostgreSQLContext> SeedSeriesAsync(
        IServiceScope scope,
        IEnumerable<ImdbImport> imports,
        IEnumerable<ImdbImportEpisode> episodes,
        IEnumerable<ImdbImportRating>? ratings = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var importList = imports.ToList();
        foreach (var row in importList)
        {
            var exists = await db.ImdbImports.AnyAsync(i => i.Tconst == row.Tconst);
            if (!exists) db.ImdbImports.Add(row);
        }

        foreach (var row in episodes)
        {
            var exists = await db.ImdbImportEpisodes.AnyAsync(e => e.Tconst == row.Tconst);
            if (!exists) db.ImdbImportEpisodes.Add(row);
        }

        await db.SaveChangesAsync();

        // Seed ratings for every seeded import so the load-phase INNER JOIN passes by default.
        // Callers may override via the ratings parameter to test filter behaviour.
        var effectiveRatings = ratings ?? importList
            .Where(i => i.TitleType is "tvSeries" or "tvMiniSeries")
            .Select(i => new ImdbImportRating { Tconst = i.Tconst, AverageRating = 7.0m, NumVotes = 5000 });

        await SeedRatingsAsync(db, effectiveRatings);

        return db;
    }

    [Fact]
    public async Task LoadAsync_LoadsMultipleSeasonsUnderOneSeries()
    {
        using var scope = Factory.Services.CreateScope();

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries", PrimaryTitle = "Multi Season Show", OriginalTitle = "Multi Season Show", StartYear = 2000, RawLine = "s1" },
                new() { Tconst = "tt1001001",   TitleType = "tvEpisode", PrimaryTitle = "S1E1", OriginalTitle = "S1E1", StartYear = 2001, RawLine = "e1" },
                new() { Tconst = "tt1002001",   TitleType = "tvEpisode", PrimaryTitle = "S2E1", OriginalTitle = "S2E1", StartYear = 2002, RawLine = "e2" },
            ],
            episodes:
            [
                new() { Tconst = "tt1001001", ParentTconst = TconstSeries1, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e1" },
                new() { Tconst = "tt1002001", ParentTconst = TconstSeries1, SeasonNumber = 2, EpisodeNumber = 1, RawLine = "e2" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var seriesRow = await db.MediaCollections.FirstOrDefaultAsync(mc => mc.ExternalId == TconstSeries1);
        seriesRow.Should().NotBeNull();

        var seasons = await db.MediaCollections
            .Where(mc => mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == seriesRow!.Id)
            .ToListAsync();

        seasons.Should().HaveCount(2);
        seasons.Should().Contain(mc => mc.Title == "1");
        seasons.Should().Contain(mc => mc.Title == "2");
    }

    [Fact]
    public async Task LoadAsync_SeasonNumberMinusOne_TitleIsUnknown()
    {
        using var scope = Factory.Services.CreateScope();

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries", PrimaryTitle = "Unknown Season Show", OriginalTitle = "Unknown Season Show", StartYear = 2000, RawLine = "s1" },
                new() { Tconst = "tt1001001",   TitleType = "tvEpisode", PrimaryTitle = "Ep1", OriginalTitle = "Ep1", StartYear = 2001, RawLine = "e1" },
            ],
            episodes:
            [
                new() { Tconst = "tt1001001", ParentTconst = TconstSeries1, SeasonNumber = -1, EpisodeNumber = 1, RawLine = "e1" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var season = await db.MediaCollections.FirstOrDefaultAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.Title == "Unknown");

        season.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_SeasonStartYear_IsMinOfEpisodeImports()
    {
        using var scope = Factory.Services.CreateScope();

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries",  PrimaryTitle = "Year Test Show", OriginalTitle = "Year Test Show", StartYear = 2000, RawLine = "s1" },
                new() { Tconst = "tt1001001",   TitleType = "tvEpisode", PrimaryTitle = "E1",             OriginalTitle = "E1",             StartYear = 2003, RawLine = "e1" },
                new() { Tconst = "tt1001002",   TitleType = "tvEpisode", PrimaryTitle = "E2",             OriginalTitle = "E2",             StartYear = 2001, RawLine = "e2" },
            ],
            episodes:
            [
                new() { Tconst = "tt1001001", ParentTconst = TconstSeries1, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e1" },
                new() { Tconst = "tt1001002", ParentTconst = TconstSeries1, SeasonNumber = 1, EpisodeNumber = 2, RawLine = "e2" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var seriesRow = await db.MediaCollections.FirstOrDefaultAsync(mc => mc.ExternalId == TconstSeries1);
        var season = await db.MediaCollections.FirstOrDefaultAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == seriesRow!.Id && mc.Title == "1");

        season.Should().NotBeNull();
        season!.ReleaseDate.Should().Be(new DateOnly(2001, 7, 1), "MIN(start_year) across season episodes is 2001");
    }

    // -------------------------------------------------------------------------
    // Episode media load tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadEpisodeMediaAsync_HappyPath_TvSeriesAndTvMiniSeries_BothLinkedToSeason()
    {
        using var scope = Factory.Services.CreateScope();

        const string miniSeriesTconst = "tt2100001";

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1,    TitleType = "tvSeries",    PrimaryTitle = "My Show",   OriginalTitle = "My Show",   StartYear = 2000, RawLine = "s1" },
                new() { Tconst = miniSeriesTconst, TitleType = "tvMiniSeries",PrimaryTitle = "Mini Show", OriginalTitle = "Mini Show", StartYear = 2015, RawLine = "s2" },
                new() { Tconst = "tt2001001",      TitleType = "tvEpisode",   PrimaryTitle = "Pilot",     OriginalTitle = "Pilot",     StartYear = 2001, RawLine = "e1" },
                new() { Tconst = "tt2100002",      TitleType = "tvEpisode",   PrimaryTitle = "Part 1",    OriginalTitle = "Part 1",    StartYear = 2015, RawLine = "e2" },
            ],
            episodes:
            [
                new() { Tconst = "tt2001001", ParentTconst = TconstSeries1,    SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e1" },
                new() { Tconst = "tt2100002", ParentTconst = miniSeriesTconst, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e2" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var tvSeriesRow   = await db.MediaCollections.FirstAsync(mc => mc.ExternalId == TconstSeries1);
        var tvSeriesSeason = await db.MediaCollections.FirstAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == tvSeriesRow.Id && mc.Title == "1");

        var tvMiniSeriesRow    = await db.MediaCollections.FirstAsync(mc => mc.ExternalId == miniSeriesTconst);
        var tvMiniSeriesSeason = await db.MediaCollections.FirstAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == tvMiniSeriesRow.Id && mc.Title == "1");

        var tvSeriesEp   = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == "tt2001001");
        var tvMiniSeriesEp = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == "tt2100002");

        tvSeriesEp.Should().NotBeNull();
        tvSeriesEp!.MediaType.Should().Be("TvShow");
        tvSeriesEp.ExternalSource.Should().Be(MediaExternalSource.Imdb);
        tvSeriesEp.ReleaseDate.Should().Be(new DateOnly(2001, 7, 1));
        tvSeriesEp.MediaCollectionId.Should().Be(tvSeriesSeason.Id);

        tvMiniSeriesEp.Should().NotBeNull();
        tvMiniSeriesEp!.MediaType.Should().Be("TvShow");
        tvMiniSeriesEp.MediaCollectionId.Should().Be(tvMiniSeriesSeason.Id);
    }

    [Fact]
    public async Task LoadEpisodeMediaAsync_SeasonMinusOne_LinksToUnknownSeason()
    {
        using var scope = Factory.Services.CreateScope();

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries",  PrimaryTitle = "Unknown Season Show", OriginalTitle = "Unknown Season Show", StartYear = 2000, RawLine = "s" },
                new() { Tconst = "tt2200001",   TitleType = "tvEpisode", PrimaryTitle = "Lost Ep",             OriginalTitle = "Lost Ep",             StartYear = 2002, RawLine = "e" },
            ],
            episodes:
            [
                new() { Tconst = "tt2200001", ParentTconst = TconstSeries1, SeasonNumber = -1, EpisodeNumber = 1, RawLine = "e" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var seriesRow  = await db.MediaCollections.FirstAsync(mc => mc.ExternalId == TconstSeries1);
        var unknownSeason = await db.MediaCollections.FirstAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == seriesRow.Id && mc.Title == "Unknown");

        var mediaRow = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == "tt2200001");

        mediaRow.Should().NotBeNull();
        mediaRow!.MediaCollectionId.Should().Be(unknownSeason.Id);
    }

    [Fact]
    public async Task LoadEpisodeMediaAsync_NullStartYear_HasNullReleaseDate()
    {
        using var scope = Factory.Services.CreateScope();

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries",  PrimaryTitle = "Null Year Show", OriginalTitle = "Null Year Show", StartYear = 2000, RawLine = "s" },
                new() { Tconst = "tt2400001",   TitleType = "tvEpisode", PrimaryTitle = "No Year Ep",     OriginalTitle = "No Year Ep",     StartYear = null, RawLine = "e" },
            ],
            episodes:
            [
                new() { Tconst = "tt2400001", ParentTconst = TconstSeries1, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var mediaRow = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == "tt2400001");

        mediaRow.Should().NotBeNull();
        mediaRow!.ReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task LoadEpisodeMediaAsync_Orphan_NoMatchingSeason_NotInserted()
    {
        using var scope = Factory.Services.CreateScope();

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries",  PrimaryTitle = "No Season Show", OriginalTitle = "No Season Show", StartYear = 2000, RawLine = "s" },
                new() { Tconst = "tt2600001",   TitleType = "tvEpisode", PrimaryTitle = "Orphan Ep",      OriginalTitle = "Orphan Ep",      StartYear = 2001, RawLine = "e" },
            ],
            episodes:
            [
                new() { Tconst = "tt2600001", ParentTconst = TconstSeries1, SeasonNumber = 99, EpisodeNumber = 1, RawLine = "e" },
            ],
            ratings: []);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();
        await loadService.LoadAsync();

        var mediaRow = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == "tt2600001");
        mediaRow.Should().BeNull("no Season collection exists with title '99' under the parent Series");
    }

    [Fact]
    public async Task LoadEpisodeMediaAsync_ReRun_SeasonRelink_UpdatesMediaCollectionId()
    {
        using var scope = Factory.Services.CreateScope();

        const string episodeTconst = "tt2700001";

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = TconstSeries1, TitleType = "tvSeries",  PrimaryTitle = "Relink Show", OriginalTitle = "Relink Show", StartYear = 2000, RawLine = "s" },
                new() { Tconst = episodeTconst, TitleType = "tvEpisode", PrimaryTitle = "Ep1",         OriginalTitle = "Ep1",         StartYear = 2001, RawLine = "e" },
                new() { Tconst = "tt2700002",   TitleType = "tvEpisode", PrimaryTitle = "Ep2 S2",      OriginalTitle = "Ep2 S2",      StartYear = 2002, RawLine = "e2" },
            ],
            episodes:
            [
                new() { Tconst = episodeTconst, ParentTconst = TconstSeries1, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e" },
                new() { Tconst = "tt2700002",   ParentTconst = TconstSeries1, SeasonNumber = 2, EpisodeNumber = 1, RawLine = "e2" },
            ]);

        var loadService = scope.ServiceProvider.GetRequiredService<ImdbLoadService>();

        // First run — episode lands in Season 1
        await loadService.LoadAsync();

        var seriesRow = await db.MediaCollections.FirstAsync(mc => mc.ExternalId == TconstSeries1);
        var season1   = await db.MediaCollections.FirstAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == seriesRow.Id && mc.Title == "1");
        var season2   = await db.MediaCollections.FirstAsync(mc =>
            mc.CollectionType == MediaCollectionType.Season && mc.ParentMediaCollectionId == seriesRow.Id && mc.Title == "2");

        var afterFirstRun = await db.Media.FirstAsync(m => m.ExternalId == episodeTconst);
        afterFirstRun.MediaCollectionId.Should().Be(season1.Id);

        // Mutate season_number from 1 → 2 in imdb_import_episodes
        await db.Database.ExecuteSqlRawAsync($"""
            UPDATE imdb_import_episodes SET season_number = 2 WHERE tconst = '{episodeTconst}'
            """);

        // Second run — upsert should relink to Season 2
        await loadService.LoadAsync();

        await db.Entry(afterFirstRun).ReloadAsync();

        var allEpisodeMedia = await db.Media.Where(m => m.ExternalId == episodeTconst).ToListAsync();
        allEpisodeMedia.Should().HaveCount(1, "upsert must not create a duplicate row");
        allEpisodeMedia[0].MediaCollectionId.Should().Be(season2.Id);
    }

    // -------------------------------------------------------------------------
    // Ratings filter tests
    // -------------------------------------------------------------------------

    private ImdbLoadService LoadServiceWithMinVotes(IServiceScope scope, int movies, int tv, int videoGames) =>
        new(scope.ServiceProvider.GetRequiredService<IImdbLoadProvider>(),
            Options.Create(new ImdbImportOptions { MinVotesMovies = movies, MinVotesTv = tv, MinVotesVideoGames = videoGames }),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<ImdbLoadService>());


    [Fact]
    public async Task LoadNonSeriesMediaAsync_ExcludesMedia_WhenNumVotesBelowMinVotes()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        const string tconst = "tt9000001";
        db.ImdbImports.Add(new() { Tconst = tconst, TitleType = "movie", PrimaryTitle = "Low Vote Movie", OriginalTitle = "Low Vote Movie", StartYear = 2020, RawLine = "r" });
        await db.SaveChangesAsync();
        await SeedRatingsAsync(db, [new() { Tconst = tconst, AverageRating = 9.0m, NumVotes = 49 }]);

        var loadService = LoadServiceWithMinVotes(scope, movies: 50, tv: 50, videoGames: 50);
        await loadService.LoadAsync();

        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == tconst);
        row.Should().BeNull("num_votes=49 is below the MinVotes=50 threshold");
    }

    [Fact]
    public async Task LoadNonSeriesMediaAsync_IncludesMedia_WhenNumVotesAtMinVotes()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        const string tconst = "tt9000002";
        db.ImdbImports.Add(new() { Tconst = tconst, TitleType = "movie", PrimaryTitle = "Exactly 50 Movie", OriginalTitle = "Exactly 50 Movie", StartYear = 2020, RawLine = "r" });
        await db.SaveChangesAsync();
        await SeedRatingsAsync(db, [new() { Tconst = tconst, AverageRating = 7.0m, NumVotes = 50 }]);

        var loadService = LoadServiceWithMinVotes(scope, movies: 50, tv: 50, videoGames: 50);
        await loadService.LoadAsync();

        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == tconst);
        row.Should().NotBeNull("num_votes=50 is exactly at the MinVotes threshold and should be included");
    }

    [Fact]
    public async Task LoadNonSeriesMediaAsync_ExcludesMedia_WhenTconstAbsentFromRatings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        const string tconst = "tt9000003";
        db.ImdbImports.Add(new() { Tconst = tconst, TitleType = "movie", PrimaryTitle = "Unrated Movie", OriginalTitle = "Unrated Movie", StartYear = 2020, RawLine = "r" });
        await db.SaveChangesAsync();
        // No matching ratings row seeded.

        var loadService = LoadServiceWithMinVotes(scope, movies: 50, tv: 50, videoGames: 50);
        await loadService.LoadAsync();

        var row = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == tconst);
        row.Should().BeNull("tconst absent from imdb_import_ratings must be excluded by INNER JOIN");
    }

    [Fact]
    public async Task LoadSeriesCollectionsAsync_ExcludesSeries_WhenNumVotesBelowMinVotes()
    {
        using var scope = Factory.Services.CreateScope();

        const string seriesTconst  = "tt9001001";
        const string episodeTconst = "tt9001002";

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = seriesTconst,  TitleType = "tvSeries",  PrimaryTitle = "Low Vote Series", OriginalTitle = "Low Vote Series", StartYear = 2020, RawLine = "s" },
                new() { Tconst = episodeTconst, TitleType = "tvEpisode", PrimaryTitle = "Ep1",             OriginalTitle = "Ep1",             StartYear = 2020, RawLine = "e" },
            ],
            episodes: [new() { Tconst = episodeTconst, ParentTconst = seriesTconst, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e" }],
            ratings:  [new() { Tconst = seriesTconst, AverageRating = 7.0m, NumVotes = 49 }]);

        var loadService = LoadServiceWithMinVotes(scope, movies: 50, tv: 50, videoGames: 50);
        await loadService.LoadAsync();

        var seriesRow = await db.MediaCollections.FirstOrDefaultAsync(mc => mc.ExternalId == seriesTconst);
        seriesRow.Should().BeNull("series with num_votes=49 is below MinVotes=50 and must be excluded");

        var episodeRow = await db.Media.FirstOrDefaultAsync(m => m.ExternalId == episodeTconst);
        episodeRow.Should().BeNull("episode must also be absent because its parent series was excluded");
    }

    [Fact]
    public async Task LoadSeriesCollectionsAsync_IncludesSeries_WhenNumVotesAtMinVotes()
    {
        using var scope = Factory.Services.CreateScope();

        const string seriesTconst  = "tt9002001";
        const string episodeTconst = "tt9002002";

        var db = await SeedSeriesAsync(scope,
            imports:
            [
                new() { Tconst = seriesTconst,  TitleType = "tvSeries",  PrimaryTitle = "Exactly 50 Series", OriginalTitle = "Exactly 50 Series", StartYear = 2020, RawLine = "s" },
                new() { Tconst = episodeTconst, TitleType = "tvEpisode", PrimaryTitle = "Ep1",               OriginalTitle = "Ep1",               StartYear = 2020, RawLine = "e" },
            ],
            episodes: [new() { Tconst = episodeTconst, ParentTconst = seriesTconst, SeasonNumber = 1, EpisodeNumber = 1, RawLine = "e" }],
            ratings:  [new() { Tconst = seriesTconst, AverageRating = 7.0m, NumVotes = 50 }]);

        var loadService = LoadServiceWithMinVotes(scope, movies: 50, tv: 50, videoGames: 50);
        await loadService.LoadAsync();

        var seriesRow = await db.MediaCollections.FirstOrDefaultAsync(mc => mc.ExternalId == seriesTconst);
        seriesRow.Should().NotBeNull("series with num_votes=50 meets MinVotes threshold and must be included");
    }

}
