using FluentAssertions;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.UnitTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ArtworkServiceTests : IDisposable
{
    private readonly PostgreSQLContext _context = TestDbContextFactory.Create();

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetMediaArtworkAsync_ManualMedia_ReturnsUnsupportedWithoutCreatingWork()
    {
        var media = await AddMediaAsync("Manual title", externalId: null, externalSource: null, mediaTypeId: -3);

        var result = await CreateService(tmdbEnabled: true).GetMediaArtworkAsync([media.Id]);

        result[media.Id].Should().BeEquivalentTo(new { Url = (string?)null, Status = "unsupported" });
        (await _context.MediaCovers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetMediaArtworkAsync_WhenTmdbIsDisabled_ReturnsDisabledWithoutCreatingWork()
    {
        var media = await AddMediaAsync("Imported movie", "tt0133093", MediaExternalSource.Imdb, -3);

        var result = await CreateService(tmdbEnabled: false).GetMediaArtworkAsync([media.Id]);

        result[media.Id].Should().BeEquivalentTo(new { Url = (string?)null, Status = "disabled" });
        (await _context.MediaCovers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetMediaArtworkAsync_RepeatedMovieDemand_CreatesOneCanonicalPendingCover()
    {
        var media = await AddMediaAsync("The Matrix", "tt0133093", MediaExternalSource.Imdb, -3);
        var service = CreateService(tmdbEnabled: true);

        var first = await service.GetMediaArtworkAsync([media.Id]);
        var second = await service.GetMediaArtworkAsync([media.Id]);

        first[media.Id].Status.Should().Be("pending");
        second[media.Id].Status.Should().Be("pending");
        var cover = await _context.MediaCovers.SingleAsync();
        cover.Provider.Should().Be(ArtworkProvider.Tmdb);
        cover.LookupKind.Should().Be(CoverLookupKind.MovieImdb);
        cover.LookupId.Should().Be("tt0133093");
        cover.RequestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMediaArtworkAsync_EpisodesInOneSeries_ShareSeriesCoverWork()
    {
        var tvType = await _context.MediaTypes.SingleAsync(type => type.Id == -4);
        var series = new MediaCollection
        {
            Title = "Example Series",
            CollectionType = MediaCollectionType.Series,
            MediaTypeId = tvType.Id,
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt0903747"
        };
        var season = new MediaCollection
        {
            Title = "Season 1",
            CollectionType = MediaCollectionType.Season,
            MediaTypeId = tvType.Id,
            ParentMediaCollection = series
        };
        _context.MediaCollections.Add(season);
        await _context.SaveChangesAsync();

        var firstEpisode = new MediaEntity
        {
            Title = "Episode 1",
            MediaTypeId = tvType.Id,
            MediaCollectionId = season.Id,
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt0959621"
        };
        var secondEpisode = new MediaEntity
        {
            Title = "Episode 2",
            MediaTypeId = tvType.Id,
            MediaCollectionId = season.Id,
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt1054724"
        };
        _context.Media.AddRange(firstEpisode, secondEpisode);
        await _context.SaveChangesAsync();

        var result = await CreateService(tmdbEnabled: true).GetMediaArtworkAsync([firstEpisode.Id, secondEpisode.Id]);

        result.Values.Should().OnlyContain(presentation => presentation.Status == "pending");
        var cover = await _context.MediaCovers.SingleAsync();
        cover.LookupKind.Should().Be(CoverLookupKind.SeriesImdb);
        cover.LookupId.Should().Be(series.ExternalId);
    }

    private async Task<MediaEntity> AddMediaAsync(string title, string? externalId, MediaExternalSource? externalSource, long mediaTypeId)
    {
        var media = new MediaEntity
        {
            Title = title,
            MediaTypeId = mediaTypeId,
            ExternalId = externalId,
            ExternalSource = externalSource,
            ReleaseDate = new DateOnly(2024, 1, 1)
        };
        _context.Media.Add(media);
        await _context.SaveChangesAsync();
        return media;
    }

    private ArtworkService CreateService(bool tmdbEnabled) => new(
        _context,
        Options.Create(new IgdbOptions()),
        Options.Create(new TmdbOptions { Enabled = tmdbEnabled, ReadAccessToken = tmdbEnabled ? "test-token" : null }),
        Options.Create(new ArtworkOptions()),
        TimeProvider.System,
        NullLogger<ArtworkService>.Instance);
}
