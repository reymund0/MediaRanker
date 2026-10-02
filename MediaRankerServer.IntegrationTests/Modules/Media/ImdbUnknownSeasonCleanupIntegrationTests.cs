using System.Data.Common;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ImdbUnknownSeasonCleanupIntegrationTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task LoadSkipsUnknownSeasonButKeepsSeasonZeroAndNullableEpisodeNumber()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        const string seriesId = "tt9500000";
        const string unknownEpisodeId = "tt9500001";
        const string zeroEpisodeId = "tt9500002";

        db.ImdbImports.AddRange(
            NewImport(seriesId, "tvSeries", "Season sentinel show"),
            NewImport(unknownEpisodeId, "tvEpisode", "Unknown season episode"),
            NewImport(zeroEpisodeId, "tvEpisode", "Season zero episode"));
        db.ImdbImportRatings.Add(new() { Tconst = seriesId, AverageRating = 8.0m, NumVotes = 5_000 });
        db.ImdbImportEpisodes.AddRange(
            new ImdbImportEpisode { Tconst = unknownEpisodeId, ParentTconst = seriesId, SeasonNumber = -1, EpisodeNumber = 1, RawLine = "unknown" },
            new ImdbImportEpisode { Tconst = zeroEpisodeId, ParentTconst = seriesId, SeasonNumber = 0, EpisodeNumber = -1, RawLine = "zero" });
        await db.SaveChangesAsync();

        await CreateLoadService(scope, maxLoadRows: 1, maxCleanupRows: 1).LoadAsync();

        var series = await db.MediaCollections.AsNoTracking().SingleAsync(c =>
            c.ExternalId == seriesId && c.CollectionType == MediaCollectionType.Series);
        var seasonZero = await db.MediaCollections.AsNoTracking().SingleAsync(c =>
            c.ParentMediaCollectionId == series.Id && c.CollectionType == MediaCollectionType.Season);
        seasonZero.Title.Should().Be("0");
        seasonZero.SeasonNumber.Should().Be(0);

        (await db.Media.AsNoTracking().AnyAsync(m => m.ExternalId == unknownEpisodeId)).Should().BeFalse();
        var episode = await db.Media.AsNoTracking().SingleAsync(m => m.ExternalId == zeroEpisodeId);
        episode.MediaCollectionId.Should().Be(seasonZero.Id);
        episode.EpisodeNumber.Should().BeNull();
    }

    [Fact]
    public async Task CleanupPagesReviewedEpisodesAndPreservesReviewedSeriesAndHandAddedEmptySeries()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var reviewedEpisodeSeries = NewSeries("tt9600001", "Reviewed episode series");
        var reviewedSeries = NewSeries("tt9600002", "Reviewed series");
        var removableSeries = NewSeries("tt9600003", "Removable series");
        var emptyUnknownSeries = NewSeries("tt9600004", "Empty unknown series");
        var handAddedSeries = NewSeries("tt9600005", "Hand added series");
        var zeroSeasonSeries = NewSeries("tt9600006", "Season zero series");

        var reviewedEpisodeSeason = NewSeason(reviewedEpisodeSeries, null, "Unknown");
        var reviewedSeriesSeason = NewSeason(reviewedSeries, null, "Unknown");
        var removableSeason = NewSeason(removableSeries, null, "Unknown");
        var emptyUnknownSeason = NewSeason(emptyUnknownSeries, null, "Unknown");
        var zeroSeason = NewSeason(zeroSeasonSeries, 0, "0");

        var reviewedEpisode = NewEpisode(98001, "tt9600101", reviewedEpisodeSeason, -1, "Reviewed episode");
        var seriesReviewEpisode = NewEpisode(98002, "tt9600102", reviewedSeriesSeason, -1, "Series review episode");
        var removableEpisode = NewEpisode(98003, "tt9600103", removableSeason, -1, "Removable episode");
        var zeroEpisode = NewEpisode(98004, "tt9600104", zeroSeason, 0, "Season zero episode");

        db.AddRange(reviewedEpisodeSeries, reviewedSeries, removableSeries, emptyUnknownSeries, handAddedSeries, zeroSeasonSeries,
            reviewedEpisodeSeason, reviewedSeriesSeason, removableSeason, emptyUnknownSeason, zeroSeason,
            reviewedEpisode, seriesReviewEpisode, removableEpisode, zeroEpisode);
        db.ImdbImports.AddRange(
            NewImport(reviewedEpisode.ExternalId!, "tvEpisode", reviewedEpisode.Title),
            NewImport(seriesReviewEpisode.ExternalId!, "tvEpisode", seriesReviewEpisode.Title),
            NewImport(removableEpisode.ExternalId!, "tvEpisode", removableEpisode.Title),
            NewImport(zeroEpisode.ExternalId!, "tvEpisode", zeroEpisode.Title));
        db.ImdbImportEpisodes.AddRange(
            NewStagedEpisode(reviewedEpisode, -1, 1),
            NewStagedEpisode(seriesReviewEpisode, -1, 1),
            NewStagedEpisode(removableEpisode, -1, 1),
            NewStagedEpisode(zeroEpisode, 0, 0));
        await db.SaveChangesAsync();

        var tvTemplateId = await db.Templates.AsNoTracking()
            .Where(t => t.MediaType == "TvShow")
            .Select(t => t.Id)
            .FirstAsync();
        db.Reviews.AddRange(
            new Review { UserId = "cleanup-episode-review", OverallScore = 8, MediaId = reviewedEpisode.Id, TemplateId = tvTemplateId },
            new Review { UserId = "cleanup-series-review", OverallScore = 9, MediaCollectionId = reviewedSeries.Id, TemplateId = tvTemplateId });
        await db.SaveChangesAsync();

        var provider = scope.ServiceProvider.GetRequiredService<IImdbLoadProvider>();
        var firstEpisodePage = await provider.DeleteUnknownSeasonEpisodesBatchAsync(null, 1, CancellationToken.None);
        firstEpisodePage.NextMediaId.Should().Be(reviewedEpisode.Id);
        firstEpisodePage.Deleted.Should().Be(0);
        firstEpisodePage.Skipped.Should().Be(1);
        firstEpisodePage.HasMore.Should().BeTrue();

        await CreateLoadService(scope, maxLoadRows: 1, maxCleanupRows: 1).LoadAsync();

        var remainingMedia = await db.Media.AsNoTracking().Select(m => m.Id).ToListAsync();
        remainingMedia.Should().Contain(reviewedEpisode.Id);
        remainingMedia.Should().Contain(zeroEpisode.Id);
        remainingMedia.Should().NotContain(seriesReviewEpisode.Id);
        remainingMedia.Should().NotContain(removableEpisode.Id);

        var remainingCollections = await db.MediaCollections.AsNoTracking().ToListAsync();
        remainingCollections.Should().Contain(c => c.Id == reviewedEpisodeSeries.Id);
        remainingCollections.Should().Contain(c => c.Id == reviewedEpisodeSeason.Id);
        remainingCollections.Should().Contain(c => c.Id == reviewedSeries.Id, "the series review prevents deleting the parent after its Unknown season is removed");
        remainingCollections.Should().NotContain(c => c.Id == reviewedSeriesSeason.Id);
        remainingCollections.Should().NotContain(c => c.Id == removableSeries.Id);
        remainingCollections.Should().NotContain(c => c.Id == removableSeason.Id);
        remainingCollections.Should().NotContain(c => c.Id == emptyUnknownSeries.Id);
        remainingCollections.Should().NotContain(c => c.Id == emptyUnknownSeason.Id);
        remainingCollections.Should().Contain(c => c.Id == handAddedSeries.Id, "series with no prior season is outside cleanup candidates");
        remainingCollections.Should().Contain(c => c.Id == zeroSeason.Id && c.SeasonNumber == 0);

        (await db.Reviews.AsNoTracking().AnyAsync(r => r.UserId == "cleanup-series-review" && r.MediaCollectionId == reviewedSeries.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ImportEpisodesUpdatesExistingStagingNumbersOnReplay()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var importer = scope.ServiceProvider.GetRequiredService<IImdbImportProvider>();

        await importer.ImportEpisodesAsync([new("tt9700001", "tt9700002", 1, 2, "first")], CancellationToken.None);
        await importer.ImportEpisodesAsync([new("tt9700001", "tt9700003", 0, 9, "updated")], CancellationToken.None);

        var episode = await db.ImdbImportEpisodes.AsNoTracking().SingleAsync(e => e.Tconst == "tt9700001");
        episode.ParentTconst.Should().Be("tt9700003");
        episode.SeasonNumber.Should().Be(0);
        episode.EpisodeNumber.Should().Be(9);
        episode.RawLine.Should().Be("updated");
    }

    [Fact]
    public async Task SeasonCleanupCancellationRollsBackBeforeParentDeleteAndCanReplay()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = NewSeries("tt9800001", "Cancellation series");
        var season = NewSeason(series, null, "Unknown");
        db.AddRange(series, season);
        await db.SaveChangesAsync();

        using var cancellation = new CancellationTokenSource();
        var options = new ImdbImportOptions { MaxStatementSeconds = 5 };
        var interceptor = new CancelAfterSeasonDeleteInterceptor(cancellation);
        var contextOptions = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(db.Database.GetDbConnection())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;
        await using (var interceptedDb = new PostgreSQLContext(contextOptions))
        {
            var provider = new ImdbLoadSqlProvider(
                interceptedDb,
                NullLogger<ImdbLoadSqlProvider>.Instance,
                Options.Create(options));
            var interrupted = () => provider.DeleteEmptyUnknownSeasonsBatchAsync(null, 10, cancellation.Token);
            await interrupted.Should().ThrowAsync<OperationCanceledException>();
        }

        (await db.MediaCollections.AsNoTracking().AnyAsync(c => c.Id == season.Id)).Should().BeTrue();
        (await db.MediaCollections.AsNoTracking().AnyAsync(c => c.Id == series.Id)).Should().BeTrue();

        var replay = new ImdbLoadSqlProvider(db, NullLogger<ImdbLoadSqlProvider>.Instance, Options.Create(options));
        var result = await replay.DeleteEmptyUnknownSeasonsBatchAsync(null, 10, CancellationToken.None);
        result.DeletedSeasons.Should().Be(1);
        result.DeletedSeries.Should().Be(1);
        (await db.MediaCollections.AsNoTracking().AnyAsync(c => c.Id == season.Id || c.Id == series.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupAcrossSeasonPagesPreservesParentWithNumberedSeason()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = NewSeries("tt9900000", "Mixed season series");
        var firstUnknown = NewSeason(series, null, "Unknown");
        var secondUnknown = NewSeason(series, null, "Legacy unknown");
        var numbered = NewSeason(series, 1, "1");
        db.AddRange(series, firstUnknown, secondUnknown, numbered);
        await db.SaveChangesAsync();

        var provider = scope.ServiceProvider.GetRequiredService<IImdbLoadProvider>();
        var first = await provider.DeleteEmptyUnknownSeasonsBatchAsync(null, 1, CancellationToken.None);
        var second = await provider.DeleteEmptyUnknownSeasonsBatchAsync(first.NextSeasonId, 1, CancellationToken.None);
        var terminal = await provider.DeleteEmptyUnknownSeasonsBatchAsync(second.NextSeasonId, 1, CancellationToken.None);

        first.DeletedSeasons.Should().Be(1);
        second.DeletedSeasons.Should().Be(1);
        first.HasMore.Should().BeTrue();
        second.HasMore.Should().BeTrue();
        terminal.HasMore.Should().BeFalse();
        (first.DeletedSeries + second.DeletedSeries + terminal.DeletedSeries).Should().Be(0);
        var remaining = await db.MediaCollections.AsNoTracking().Select(c => c.Id).ToListAsync();
        remaining.Should().BeEquivalentTo(new[] { series.Id, numbered.Id });
    }

    private static ImdbImport NewImport(string tconst, string titleType, string title) => new()
    {
        Tconst = tconst,
        TitleType = titleType,
        PrimaryTitle = title,
        OriginalTitle = title,
        StartYear = 2020,
        RawLine = title
    };

    private static MediaCollection NewSeries(string tconst, string title) => new()
    {
        Title = title,
        CollectionType = MediaCollectionType.Series,
        MediaType = "TvShow",
        ExternalId = tconst,
        ExternalSource = MediaExternalSource.Imdb
    };

    private static MediaCollection NewSeason(MediaCollection series, int? seasonNumber, string title) => new()
    {
        Title = title,
        CollectionType = MediaCollectionType.Season,
        MediaType = "TvShow",
        ExternalId = series.ExternalId,
        ExternalSource = MediaExternalSource.Imdb,
        SeasonNumber = seasonNumber,
        ParentMediaCollection = series
    };

    private static MediaEntity NewEpisode(long id, string tconst, MediaCollection season, int? episodeNumber, string title) => new()
    {
        Id = id,
        Title = title,
        MediaType = "TvShow",
        ExternalId = tconst,
        ExternalSource = MediaExternalSource.Imdb,
        EpisodeNumber = episodeNumber,
        MediaCollection = season
    };

    private static ImdbImportEpisode NewStagedEpisode(MediaEntity episode, int seasonNumber, int episodeNumber) => new()
    {
        Tconst = episode.ExternalId!,
        ParentTconst = episode.MediaCollection!.ParentMediaCollection!.ExternalId!,
        SeasonNumber = seasonNumber,
        EpisodeNumber = episodeNumber,
        RawLine = episode.Title
    };

    private static ImdbLoadService CreateLoadService(IServiceScope scope, int maxLoadRows, int maxCleanupRows) => new(
        scope.ServiceProvider.GetRequiredService<IImdbLoadProvider>(),
        Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = maxLoadRows, MaxCleanupRowsPerUnit = maxCleanupRows }),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<ImdbLoadService>());

    private sealed class CancelAfterSeasonDeleteInterceptor(CancellationTokenSource cancellation) : DbCommandInterceptor
    {
        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("DELETE FROM media_collections season", StringComparison.Ordinal))
                cancellation.Cancel();
            return ValueTask.FromResult(result);
        }
    }
}
