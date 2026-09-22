using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ArtworkServiceIntegrationTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task GetMediaArtworkAsync_EpisodesFromOneSeriesShareTheCanonicalSeriesPoster()
    {
        long firstEpisodeId;
        long secondEpisodeId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var series = new MediaCollection
            {
                Title = "Example Series",
                CollectionType = MediaCollectionType.Series,
                MediaTypeId = -4,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0903747"
            };
            var season = new MediaCollection
            {
                Title = "Season 1",
                CollectionType = MediaCollectionType.Season,
                MediaTypeId = -4,
                ParentMediaCollection = series
            };
            db.MediaCollections.Add(season);
            await db.SaveChangesAsync();

            db.MediaCovers.Add(new MediaCover
            {
                Provider = ArtworkProvider.Tmdb,
                LookupKind = CoverLookupKind.SeriesImdb,
                LookupId = series.ExternalId!,
                Outcome = CoverOutcome.Ready,
                ProviderItemId = "1396",
                ImagePath = "/series.jpg",
                CheckedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            });
            var firstEpisode = new MediaEntity
            {
                Title = "Episode 1",
                MediaTypeId = -4,
                MediaCollectionId = season.Id,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0959621"
            };
            var secondEpisode = new MediaEntity
            {
                Title = "Episode 2",
                MediaTypeId = -4,
                MediaCollectionId = season.Id,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt1054724"
            };
            db.Media.AddRange(firstEpisode, secondEpisode);
            await db.SaveChangesAsync();
            firstEpisodeId = firstEpisode.Id;
            secondEpisodeId = secondEpisode.Id;
        }

        using var artworkScope = Factory.Services.CreateScope();
        var artwork = artworkScope.ServiceProvider.GetRequiredService<IArtworkService>();
        var presentations = await artwork.GetMediaArtworkAsync([firstEpisodeId, secondEpisodeId]);

        presentations[firstEpisodeId].Status.Should().Be("ready");
        presentations[secondEpisodeId].Status.Should().Be("ready");
        presentations[firstEpisodeId].Url.Should().Be("https://image.tmdb.org/t/p/w342/series.jpg");
        presentations[secondEpisodeId].Url.Should().Be(presentations[firstEpisodeId].Url);
    }

    [Fact]
    public async Task GetMediaArtworkAsync_WhenProviderIsDisabled_ReturnsDisabledWithoutCreatingWork()
    {
        long mediaId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var media = new MediaEntity
            {
                Title = "Imported Movie",
                MediaTypeId = -3,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0133093"
            };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            mediaId = media.Id;
        }

        using var artworkScope = Factory.Services.CreateScope();
        var artwork = artworkScope.ServiceProvider.GetRequiredService<IArtworkService>();
        var presentations = await artwork.GetMediaArtworkAsync([mediaId]);

        presentations[mediaId].Url.Should().BeNull();
        presentations[mediaId].Status.Should().Be("disabled");
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        (await verifyDb.MediaCovers.CountAsync()).Should().Be(0);
    }
}
