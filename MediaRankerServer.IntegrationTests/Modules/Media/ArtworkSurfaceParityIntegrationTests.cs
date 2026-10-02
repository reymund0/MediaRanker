using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ArtworkSurfaceParityIntegrationTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const string UserId = "test-user-1";

    [Fact]
    public async Task MediaCollectionUnreviewedAndReviewSurfaces_ExposeTheSameCoverContract()
    {
        await SeedFixtureAsync();

        var reviewedMovie = await GetMediaAsync("Movie", "Parity reviewed movie");
        var unreviewedMovie = await GetMediaAsync("Movie", "Parity unreviewed movie");
        var game = await GetMediaAsync("VideoGame", "Parity game");
        var reviewedEpisode = await GetMediaAsync("TvShow", "Parity reviewed episode");
        var unreviewedEpisode = await GetMediaAsync("TvShow", "Parity unreviewed episode");
        var series = await GetCollectionAsync("Parity TV series");

        var unreviewedMovieSurface = await GetUnreviewedAsync("Movie", "Parity unreviewed movie");
        var unreviewedGameSurface = await GetUnreviewedAsync("VideoGame", "Parity game");
        var unreviewedEpisodeSurface = await GetUnreviewedAsync("TvShow", "Parity unreviewed episode");
        var movieReviewSurface = (await GetReviewsAsync("Movie")).Single(review => review.MediaTitle == "Parity reviewed movie");
        var tvReviewSurface = (await GetReviewsAsync("TvShow")).Single(review => review.MediaTitle == "Parity reviewed episode");

        AssertCover(reviewedMovie.CoverImageUrl, reviewedMovie.CoverStatus,
            "https://image.tmdb.org/t/p/w342/movie-reviewed.jpg", "ready");
        AssertCover(unreviewedMovie.CoverImageUrl, unreviewedMovie.CoverStatus,
            "https://image.tmdb.org/t/p/w342/movie-unreviewed.jpg", "ready");
        AssertCover(game.CoverImageUrl, game.CoverStatus,
            "https://images.igdb.com/igdb/image/upload/t_cover_big/game-cover.jpg", "ready");
        AssertCover(reviewedEpisode.CoverImageUrl, reviewedEpisode.CoverStatus,
            "https://image.tmdb.org/t/p/w342/tv-series.jpg", "ready");
        AssertCover(unreviewedEpisode.CoverImageUrl, unreviewedEpisode.CoverStatus,
            "https://image.tmdb.org/t/p/w342/tv-series.jpg", "ready");
        AssertCover(series.CoverImageUrl, series.CoverStatus,
            "https://image.tmdb.org/t/p/w342/tv-series.jpg", "ready");

        AssertCover(unreviewedMovieSurface.CoverImageUrl, unreviewedMovieSurface.CoverStatus,
            unreviewedMovie.CoverImageUrl, unreviewedMovie.CoverStatus);
        AssertCover(unreviewedGameSurface.CoverImageUrl, unreviewedGameSurface.CoverStatus,
            game.CoverImageUrl, game.CoverStatus);
        AssertCover(unreviewedEpisodeSurface.CoverImageUrl, unreviewedEpisodeSurface.CoverStatus,
            unreviewedEpisode.CoverImageUrl, unreviewedEpisode.CoverStatus);
        AssertCover(movieReviewSurface.MediaCoverImageUrl, movieReviewSurface.CoverStatus,
            reviewedMovie.CoverImageUrl, reviewedMovie.CoverStatus);
        AssertCover(tvReviewSurface.MediaCoverImageUrl, tvReviewSurface.CoverStatus,
            reviewedEpisode.CoverImageUrl, reviewedEpisode.CoverStatus);
    }

    private async Task SeedFixtureAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;

        var movieReviewedCover = CreateTmdbCover("tt1000001", "/movie-reviewed.jpg", "101", now);
        var movieUnreviewedCover = CreateTmdbCover("tt1000002", "/movie-unreviewed.jpg", "102", now);
        var gameCover = CreateIgdbCover("42", "game-cover", "42", now);
        var tvCover = CreateTmdbCover("tt1000003", "/tv-series.jpg", "103", now);

        var movieReviewed = new MediaEntity
        {
            Title = "Parity reviewed movie",
            MediaType = "Movie",
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt1000001",
            ReleaseDate = new DateOnly(2020, 1, 1),
            Cover = movieReviewedCover
        };
        var movieUnreviewed = new MediaEntity
        {
            Title = "Parity unreviewed movie",
            MediaType = "Movie",
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt1000002",
            ReleaseDate = new DateOnly(2020, 1, 2),
            Cover = movieUnreviewedCover
        };
        var game = new MediaEntity
        {
            Title = "Parity game",
            MediaType = "VideoGame",
            ExternalSource = MediaExternalSource.Igdb,
            ExternalId = "42",
            ReleaseDate = new DateOnly(2020, 1, 3),
            Cover = gameCover
        };
        var series = new MediaCollection
        {
            Title = "Parity TV series",
            CollectionType = MediaCollectionType.Series,
            MediaType = "TvShow",
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt1000003",
            Cover = tvCover
        };
        var season = new MediaCollection
        {
            Title = "Parity TV season",
            SeasonNumber = 1,
            CollectionType = MediaCollectionType.Season,
            MediaType = "TvShow",
            ParentMediaCollection = series
        };
        var reviewedEpisode = new MediaEntity
        {
            Title = "Parity reviewed episode",
            EpisodeNumber = 1,
            MediaType = "TvShow",
            MediaCollection = season,
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt1000004",
            ReleaseDate = new DateOnly(2020, 1, 4)
        };
        var unreviewedEpisode = new MediaEntity
        {
            Title = "Parity unreviewed episode",
            EpisodeNumber = 2,
            MediaType = "TvShow",
            MediaCollection = season,
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt1000005",
            ReleaseDate = new DateOnly(2020, 1, 5)
        };

        var movieTemplate = CreateTemplate("Parity movie template", "Movie", "Movie score");
        var tvTemplate = CreateTemplate("Parity TV template", "TvShow", "TV score");
        db.AddRange(movieReviewed, movieUnreviewed, game, reviewedEpisode, unreviewedEpisode,
            series, season, movieReviewedCover, movieUnreviewedCover, gameCover, tvCover,
            movieTemplate, tvTemplate);
        await db.SaveChangesAsync();

        db.Reviews.AddRange(
            new Review
            {
                UserId = UserId,
                MediaId = movieReviewed.Id,
                TemplateId = movieTemplate.Id,
                OverallScore = 8,
                Fields = [new ReviewField { TemplateFieldId = movieTemplate.Fields.Single().Id, Value = 8 }]
            },
            new Review
            {
                UserId = UserId,
                MediaId = reviewedEpisode.Id,
                TemplateId = tvTemplate.Id,
                OverallScore = 7,
                Fields = [new ReviewField { TemplateFieldId = tvTemplate.Fields.Single().Id, Value = 7 }]
            });
        await db.SaveChangesAsync();
    }

    private async Task<MediaDto> GetMediaAsync(string mediaType, string title)
    {
        var response = await Client.GetAsync(
            $"/api/media?mediaType={mediaType}&searchField=title&searchTerm={Uri.EscapeDataString(title)}&page=0&pageSize=10&sortField=title&sortDirection=asc");
        response.IsSuccessStatusCode.Should().BeTrue();
        var page = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        return page!.Items.Single(item => item.Title == title);
    }

    private async Task<MediaCollectionDto> GetCollectionAsync(string title)
    {
        var response = await Client.GetAsync(
            $"/api/mediacollection?searchField=title&searchTerm={Uri.EscapeDataString(title)}&page=0&pageSize=10&sortField=title&sortDirection=asc");
        response.IsSuccessStatusCode.Should().BeTrue();
        var page = await response.Content.ReadFromJsonAsync<PageResult<MediaCollectionDto>>();
        return page!.Items.Single(item => item.Title == title);
    }

    private async Task<UnreviewedMediaDto> GetUnreviewedAsync(string mediaType, string title)
    {
        var response = await Client.GetAsync(
            $"/api/reviews/unreviewedByType?mediaType={mediaType}&searchField=title&searchTerm={Uri.EscapeDataString(title)}&page=0&pageSize=10&sortField=title&sortDirection=asc");
        response.IsSuccessStatusCode.Should().BeTrue();
        var page = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();
        return page!.Items.Single(item => item.Title == title);
    }

    private async Task<List<ReviewDto>> GetReviewsAsync(string mediaType)
    {
        var response = await Client.GetAsync($"/api/reviews/byMediaType/{mediaType}");
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<List<ReviewDto>>())!;
    }

    private static Template CreateTemplate(string name, string mediaType, string fieldName) => new()
    {
        UserId = UserId,
        Name = name,
        MediaType = mediaType,
        Fields = [new TemplateField { Name = fieldName, Position = 0 }]
    };

    private static MediaCover CreateTmdbCover(string lookupId, string imagePath, string providerItemId, DateTimeOffset now) => new()
    {
        Provider = ArtworkProvider.Tmdb,
        LookupKind = lookupId == "tt1000003" ? CoverLookupKind.SeriesImdb : CoverLookupKind.MovieImdb,
        LookupId = lookupId,
        ProviderItemId = providerItemId,
        ImagePath = imagePath,
        Outcome = CoverOutcome.Ready,
        CheckedAt = now,
        ExpiresAt = now.AddDays(30)
    };

    private static MediaCover CreateIgdbCover(string lookupId, string imagePath, string providerItemId, DateTimeOffset now) => new()
    {
        Provider = ArtworkProvider.Igdb,
        LookupKind = CoverLookupKind.IgdbGame,
        LookupId = lookupId,
        ProviderItemId = providerItemId,
        ImagePath = imagePath,
        Outcome = CoverOutcome.Ready,
        CheckedAt = now,
        ExpiresAt = now.AddDays(30)
    };

    private static void AssertCover(string? actualUrl, string actualStatus, string? expectedUrl, string expectedStatus)
    {
        actualUrl.Should().Be(expectedUrl);
        actualStatus.Should().Be(expectedStatus);
    }
}
