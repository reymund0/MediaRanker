using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public sealed class ImdbEpisodePagingTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task EmptyCandidatePageAdvancesToLaterAdmittedEpisodeAndTerminates()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = await AddSeriesWithSeasonAsync(db, "tt9100000");

        db.ImdbImports.AddRange(
            EpisodeImport("tt9100001", "No admitted parent"),
            EpisodeImport("tt9999999", "Later eligible episode"));
        db.ImdbImportEpisodes.AddRange(
            EpisodeLink("tt9100001", "tt9000000", 1),
            EpisodeLink("tt9999999", series.ExternalId!, 1));
        await db.SaveChangesAsync();

        var provider = CreateProvider(db, batchSize: 1);
        var first = await provider.LoadEpisodeMediaBatchAsync(null, 1, CancellationToken.None);
        var second = await provider.LoadEpisodeMediaBatchAsync(first.NextKey, 1, CancellationToken.None);
        var terminal = await provider.LoadEpisodeMediaBatchAsync(second.NextKey, 1, CancellationToken.None);

        first.Affected.Should().Be(0);
        first.NextKey.Should().Be("tt9100001");
        first.HasMore.Should().BeTrue();
        second.Affected.Should().Be(1);
        second.NextKey.Should().Be("tt9999999");
        second.HasMore.Should().BeTrue();
        terminal.Affected.Should().Be(0);
        terminal.NextKey.Should().BeNull();
        terminal.HasMore.Should().BeFalse();
        (await db.Media.AsNoTracking().Where(media => media.ExternalId == "tt9999999").CountAsync()).Should().Be(1);
        (await db.Media.AsNoTracking().AnyAsync(media => media.ExternalId == "tt9100001")).Should().BeFalse();
    }

    [Fact]
    public async Task PartialFinalPageUsesLastScannedKeyAndReplayPreservesIdentityAndRelinksSeason()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = await AddSeriesWithSeasonAsync(db, "tt9200000", includeOldSeason: true);
        var oldSeason = await db.MediaCollections.SingleAsync(collection =>
            collection.ParentMediaCollectionId == series.Id && collection.Title == "9");
        var destinationSeason = await db.MediaCollections.SingleAsync(collection =>
            collection.ParentMediaCollectionId == series.Id && collection.Title == "1");

        db.ImdbImports.AddRange(
            EpisodeImport("tt9200001", "Refreshed episode", 2001),
            EpisodeImport("tt9200099", "Unlinked candidate"));
        db.ImdbImportEpisodes.Add(EpisodeLink("tt9200001", series.ExternalId!, 1));
        var existing = new MediaEntity
        {
            Title = "Old title",
            ExternalId = "tt9200001",
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4,
            MediaCollectionId = oldSeason.Id
        };
        db.Media.Add(existing);
        await db.SaveChangesAsync();

        var provider = CreateProvider(db, batchSize: 3);
        var page = await provider.LoadEpisodeMediaBatchAsync(null, 3, CancellationToken.None);

        page.Affected.Should().Be(1);
        page.NextKey.Should().Be("tt9200099", "the cursor follows the last scanned basics row even when it is not eligible");
        page.HasMore.Should().BeFalse("fewer than three candidate rows remain");

        var identity = existing.Id;
        var replay = await provider.LoadEpisodeMediaAsync(CancellationToken.None);
        replay.Affected.Should().Be(1);

        var rows = await db.Media.AsNoTracking().Where(media => media.ExternalId == "tt9200001").ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(identity);
        rows[0].ExternalSource.Should().Be(MediaExternalSource.Imdb);
        rows[0].Title.Should().Be("Refreshed episode");
        rows[0].ReleaseDate.Should().Be(new DateOnly(2001, 7, 1));
        rows[0].MediaCollectionId.Should().Be(destinationSeason.Id);
    }

    [Fact]
    public async Task SameNamedSeasonOfAnotherMediaTypeDoesNotMultiplyEpisodeOutput()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = await AddSeriesWithSeasonAsync(db, "tt9300000");
        var tvSeason = await db.MediaCollections.SingleAsync(collection =>
            collection.ParentMediaCollectionId == series.Id && collection.Title == "1");
        db.MediaCollections.Add(new MediaCollection
        {
            Title = "1",
            CollectionType = MediaCollectionType.Season,
            ParentMediaCollectionId = series.Id,
            MediaTypeId = -3
        });
        db.ImdbImports.Add(EpisodeImport("tt9300001", "Bounded episode"));
        db.ImdbImportEpisodes.Add(EpisodeLink("tt9300001", series.ExternalId!, 1));
        await db.SaveChangesAsync();

        var page = await CreateProvider(db, batchSize: 1)
            .LoadEpisodeMediaBatchAsync(null, 1, CancellationToken.None);

        page.Affected.Should().Be(1);
        page.NextKey.Should().Be("tt9300001");
        var episode = await db.Media.AsNoTracking().SingleAsync(media => media.ExternalId == "tt9300001");
        episode.MediaCollectionId.Should().Be(tvSeason.Id);
    }

    private static ImdbLoadSqlProvider CreateProvider(PostgreSQLContext db, int batchSize) =>
        new(db, NullLogger<ImdbLoadSqlProvider>.Instance,
            Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = batchSize }));

    private static async Task<MediaCollection> AddSeriesWithSeasonAsync(
        PostgreSQLContext db,
        string tconst,
        bool includeOldSeason = false)
    {
        var series = new MediaCollection
        {
            Title = "Paging series",
            CollectionType = MediaCollectionType.Series,
            ExternalId = tconst,
            ExternalSource = MediaExternalSource.Imdb,
            MediaTypeId = -4
        };
        db.MediaCollections.Add(series);
        await db.SaveChangesAsync();

        var seasons = new List<MediaCollection>
        {
            new()
            {
                Title = "1",
                CollectionType = MediaCollectionType.Season,
                ParentMediaCollectionId = series.Id,
                ExternalId = tconst,
                ExternalSource = MediaExternalSource.Imdb,
                MediaTypeId = -4
            }
        };
        if (includeOldSeason)
            seasons.Add(new MediaCollection
            {
                Title = "9",
                CollectionType = MediaCollectionType.Season,
                ParentMediaCollectionId = series.Id,
                ExternalId = tconst,
                ExternalSource = MediaExternalSource.Imdb,
                MediaTypeId = -4
            });
        db.MediaCollections.AddRange(seasons);
        await db.SaveChangesAsync();
        return series;
    }

    private static ImdbImport EpisodeImport(string tconst, string title, int? startYear = 2000) => new()
    {
        Tconst = tconst,
        TitleType = "tvEpisode",
        PrimaryTitle = title,
        OriginalTitle = title,
        StartYear = startYear,
        RawLine = tconst
    };

    private static ImdbImportEpisode EpisodeLink(string tconst, string parentTconst, int season) => new()
    {
        Tconst = tconst,
        ParentTconst = parentTconst,
        SeasonNumber = season,
        EpisodeNumber = 1,
        RawLine = tconst
    };
}
