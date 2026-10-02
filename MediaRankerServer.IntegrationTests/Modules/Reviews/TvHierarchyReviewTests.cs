using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Reviews;

public class TvHierarchyReviewTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const string TvShow = "TvShow";
    private const string Api = "/api/Reviews";

    [Fact]
    public async Task CreateSeriesAndEpisodeReviews_ReturnsHierarchyContextYearsCountsAndSharedSeriesCover()
    {
        long templateId;
        long fieldId;
        long seriesId;
        long episodeId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var template = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == TvShow);
            templateId = template.Id;
            fieldId = template.Fields.First().Id;
            var series = new MediaCollection
            {
                Title = "Hierarchy Review Series", MediaType = TvShow, CollectionType = MediaCollectionType.Series,
                ReleaseDate = new DateOnly(2007, 1, 1), ExternalSource = MediaExternalSource.Imdb, ExternalId = "tt0903747"
            };
            var seasonOne = new MediaCollection
            {
                Title = "1", MediaType = TvShow, CollectionType = MediaCollectionType.Season, SeasonNumber = 1,
                ReleaseDate = new DateOnly(2009, 1, 1), ParentMediaCollection = series
            };
            var seasonTwo = new MediaCollection
            {
                Title = "2", MediaType = TvShow, CollectionType = MediaCollectionType.Season, SeasonNumber = 2,
                ReleaseDate = new DateOnly(2012, 1, 1), ParentMediaCollection = series
            };
            db.MediaCollections.AddRange(seasonOne, seasonTwo);
            await db.SaveChangesAsync();
            var poster = new MediaCover
            {
                Provider = ArtworkProvider.Tmdb, LookupKind = CoverLookupKind.SeriesImdb, LookupId = series.ExternalId!,
                Outcome = CoverOutcome.Ready, ProviderItemId = "1396", ImagePath = "/hierarchy-review.jpg",
                CheckedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            };
            var episode = new MediaEntity
            {
                Title = "Review Episode", MediaType = TvShow, MediaCollectionId = seasonOne.Id,
                EpisodeNumber = 3, ReleaseDate = new DateOnly(2009, 4, 1)
            };
            db.MediaCovers.Add(poster);
            db.Media.Add(episode);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            episodeId = episode.Id;
        }

        var seriesResponse = await Client.PostAsJsonAsync(Api, NewRequest(templateId, fieldId, mediaCollectionId: seriesId));
        seriesResponse.EnsureSuccessStatusCode();
        var seriesReview = await seriesResponse.Content.ReadFromJsonAsync<ReviewDto>();
        seriesReview.Should().NotBeNull();
        seriesReview!.Kind.Should().Be("Series");
        seriesReview.MediaId.Should().BeNull();
        seriesReview.MediaCollectionId.Should().Be(seriesId);
        seriesReview.SeriesId.Should().Be(seriesId);
        seriesReview.SeriesTitle.Should().Be("Hierarchy Review Series");
        seriesReview.SeriesStartYear.Should().Be(2007);
        seriesReview.SeriesEndYear.Should().Be(2012);
        seriesReview.SeasonCount.Should().Be(2);
        seriesReview.EpisodeCount.Should().Be(1);
        seriesReview.MediaReleaseDate.Should().Be(new DateOnly(2007, 1, 1));
        seriesReview.MediaCoverImageUrl.Should().Be("https://image.tmdb.org/t/p/w342/hierarchy-review.jpg");

        var episodeResponse = await Client.PostAsJsonAsync(Api, NewRequest(templateId, fieldId, mediaId: episodeId));
        episodeResponse.EnsureSuccessStatusCode();
        var episodeReview = await episodeResponse.Content.ReadFromJsonAsync<ReviewDto>();
        episodeReview.Should().NotBeNull();
        episodeReview!.Kind.Should().Be("Episode");
        episodeReview.MediaId.Should().Be(episodeId);
        episodeReview.MediaCollectionId.Should().BeNull();
        episodeReview.SeriesId.Should().Be(seriesId);
        episodeReview.SeriesTitle.Should().Be("Hierarchy Review Series");
        episodeReview.SeasonNumber.Should().Be(1);
        episodeReview.EpisodeNumber.Should().Be(3);
        episodeReview.SeriesStartYear.Should().Be(2007);
        episodeReview.SeriesEndYear.Should().Be(2012);
        episodeReview.SeasonCount.Should().Be(2);
        episodeReview.EpisodeCount.Should().Be(1);
        episodeReview.MediaCoverImageUrl.Should().Be(seriesReview.MediaCoverImageUrl);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        (await verifyDb.Reviews.AnyAsync(review => review.Id == seriesReview.Id && review.MediaId == null && review.MediaCollectionId == seriesId)).Should().BeTrue();
        (await verifyDb.Reviews.AnyAsync(review => review.Id == episodeReview.Id && review.MediaId == episodeId && review.MediaCollectionId == null)).Should().BeTrue();
    }

    [Fact]
    public async Task InsertRejectsMissingBothSeasonAndNonTvTargetsMismatchedTemplateAndDuplicateSeries()
    {
        long tvTemplateId;
        long tvFieldId;
        long movieTemplateId;
        long movieFieldId;
        long seriesId;
        long seasonId;
        long movieSeriesId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var tvTemplate = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == TvShow);
            var movieTemplate = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == "Movie");
            tvTemplateId = tvTemplate.Id;
            tvFieldId = tvTemplate.Fields.First().Id;
            movieTemplateId = movieTemplate.Id;
            movieFieldId = movieTemplate.Fields.First().Id;
            var series = new MediaCollection { Title = "Review Target Series", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            var movieSeries = new MediaCollection { Title = "Review Target Movie Collection", MediaType = "Movie", CollectionType = MediaCollectionType.Series };
            var season = new MediaCollection { Title = "1", MediaType = TvShow, CollectionType = MediaCollectionType.Season, SeasonNumber = 1, ParentMediaCollection = series };
            db.MediaCollections.AddRange(season, movieSeries);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            seasonId = season.Id;
            movieSeriesId = movieSeries.Id;
        }

        var missingTarget = await PostInvalidAsync(NewRequest(tvTemplateId, tvFieldId));
        missingTarget.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        missingTarget.Status.Should().Be((int)HttpStatusCode.BadRequest);
        var bothTargets = await PostInvalidAsync(NewRequest(tvTemplateId, tvFieldId, mediaId: 12345, mediaCollectionId: seriesId));
        bothTargets.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        bothTargets.Status.Should().Be((int)HttpStatusCode.BadRequest);
        (await PostInvalidAsync(NewRequest(tvTemplateId, tvFieldId, mediaCollectionId: seasonId))).Type.Should().Be("review_insert_validation_error");
        (await PostInvalidAsync(NewRequest(tvTemplateId, tvFieldId, mediaCollectionId: movieSeriesId))).Type.Should().Be("review_insert_validation_error");
        (await PostInvalidAsync(NewRequest(movieTemplateId, movieFieldId, mediaCollectionId: seriesId))).Type.Should().Be("review_media_type_mismatch");

        var first = await Client.PostAsJsonAsync(Api, NewRequest(tvTemplateId, tvFieldId, mediaCollectionId: seriesId));
        first.EnsureSuccessStatusCode();
        var duplicate = await PostInvalidAsync(NewRequest(tvTemplateId, tvFieldId, mediaCollectionId: seriesId));
        duplicate.Type.Should().Be("review_insert_duplicate_review");
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        (await verifyDb.Reviews.CountAsync(review => review.MediaCollectionId == seriesId)).Should().Be(1);
    }

    [Fact]
    public async Task TvSeriesAndEpisodeReviewsCanUpdateAndDelete_WhileMovieAndGameReviewsRemainTitleReviews()
    {
        long tvTemplateId;
        long tvFieldId;
        long movieTemplateId;
        long movieFieldId;
        long gameTemplateId;
        long gameFieldId;
        long seriesId;
        long episodeId;
        long movieId;
        long gameId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var tvTemplate = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == TvShow);
            var movieTemplate = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == "Movie");
            var gameTemplate = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == "VideoGame");
            tvTemplateId = tvTemplate.Id;
            tvFieldId = tvTemplate.Fields.First().Id;
            movieTemplateId = movieTemplate.Id;
            movieFieldId = movieTemplate.Fields.First().Id;
            gameTemplateId = gameTemplate.Id;
            gameFieldId = gameTemplate.Fields.First().Id;
            var series = new MediaCollection { Title = "TV update/delete", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            var season = new MediaCollection { Title = "1", MediaType = TvShow, CollectionType = MediaCollectionType.Season, SeasonNumber = 1, ParentMediaCollection = series };
            var movie = new MediaEntity { Title = "Movie review regression", MediaType = "Movie" };
            var game = new MediaEntity { Title = "Game review regression", MediaType = "VideoGame" };
            db.MediaCollections.Add(season);
            db.Media.AddRange(movie, game);
            await db.SaveChangesAsync();
            var episode = new MediaEntity { Title = "Episode update/delete", MediaType = TvShow, MediaCollectionId = season.Id, EpisodeNumber = 1 };
            db.Media.Add(episode);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            episodeId = episode.Id;
            movieId = movie.Id;
            gameId = game.Id;
        }

        var seriesReview = await CreateReviewAsync(NewRequest(tvTemplateId, tvFieldId, mediaCollectionId: seriesId));
        var episodeReview = await CreateReviewAsync(NewRequest(tvTemplateId, tvFieldId, mediaId: episodeId));
        var movieReview = await CreateReviewAsync(NewRequest(movieTemplateId, movieFieldId, mediaId: movieId));
        var gameReview = await CreateReviewAsync(NewRequest(gameTemplateId, gameFieldId, mediaId: gameId));
        seriesReview.Kind.Should().Be("Series");
        episodeReview.Kind.Should().Be("Episode");
        movieReview.Kind.Should().Be("Title");
        gameReview.Kind.Should().Be("Title");
        movieReview.MediaId.Should().Be(movieId);
        gameReview.MediaId.Should().Be(gameId);

        foreach (var review in new[] { seriesReview, episodeReview })
        {
            var updated = await Client.PatchAsJsonAsync($"{Api}/update", new ReviewUpdateRequest
            {
                Id = review.Id,
                ReviewTitle = "Updated hierarchy review",
                Fields = [new ReviewFieldUpdateRequest { TemplateFieldId = review.Fields[0].TemplateFieldId, Value = 8 }]
            });
            updated.EnsureSuccessStatusCode();
            (await updated.Content.ReadFromJsonAsync<ReviewDto>())!.ReviewTitle.Should().Be("Updated hierarchy review");
        }

        foreach (var review in new[] { seriesReview, episodeReview, movieReview, gameReview })
        {
            var deleted = await Client.DeleteAsync($"{Api}/{review.Id}");
            deleted.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task SeriesDeleteHandlerRemovesAllUsersSeriesAndEpisodeReviewsAndFieldsButPreservesUnrelatedReview()
    {
        long seriesId;
        long unrelatedReviewId;
        long seriesReviewId;
        long firstEpisodeReviewId;
        long secondEpisodeReviewId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var template = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == TvShow);
            var fieldId = template.Fields.First().Id;
            var series = new MediaCollection { Title = "Series cascade removal", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            var season = new MediaCollection { Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollection = series };
            var unrelatedMedia = new MediaEntity { Title = "Unrelated movie", MediaType = "Movie" };
            db.MediaCollections.Add(season);
            db.Media.Add(unrelatedMedia);
            await db.SaveChangesAsync();
            var episodes = new[]
            {
                new MediaEntity { Title = "Episode A", MediaType = TvShow, MediaCollectionId = season.Id },
                new MediaEntity { Title = "Episode B", MediaType = TvShow, MediaCollectionId = season.Id }
            };
            db.Media.AddRange(episodes);
            await db.SaveChangesAsync();
            var seriesReview = NewStoredReview("series-review-owner", template.Id, fieldId, mediaCollectionId: series.Id);
            var firstEpisodeReview = NewStoredReview("episode-review-owner", template.Id, fieldId, mediaId: episodes[0].Id);
            var secondEpisodeReview = NewStoredReview("other-user-episode-review", template.Id, fieldId, mediaId: episodes[1].Id);
            var unrelatedReview = NewStoredReview("unrelated-review-owner", template.Id, fieldId, mediaId: unrelatedMedia.Id);
            db.Reviews.AddRange(seriesReview, firstEpisodeReview, secondEpisodeReview, unrelatedReview);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            seriesReviewId = seriesReview.Id;
            firstEpisodeReviewId = firstEpisodeReview.Id;
            secondEpisodeReviewId = secondEpisodeReview.Id;
            unrelatedReviewId = unrelatedReview.Id;
        }

        var response = await Client.DeleteAsync($"/api/mediacollection/{seriesId}");
        response.EnsureSuccessStatusCode();
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        (await verifyDb.Reviews.AnyAsync(review => review.Id == seriesReviewId || review.Id == firstEpisodeReviewId || review.Id == secondEpisodeReviewId)).Should().BeFalse();
        (await verifyDb.ReviewFields.AnyAsync(field => field.ReviewId == seriesReviewId || field.ReviewId == firstEpisodeReviewId || field.ReviewId == secondEpisodeReviewId)).Should().BeFalse();
        (await verifyDb.Reviews.AnyAsync(review => review.Id == unrelatedReviewId)).Should().BeTrue();
    }

    [Fact]
    public async Task LegacyUnreviewedMediaDoesNotReturnUnknownSeasonEpisodes()
    {
        long numberedEpisodeId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var series = new MediaCollection { Title = "Legacy picker hierarchy", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            var numberedSeason = new MediaCollection { Title = "1", MediaType = TvShow, CollectionType = MediaCollectionType.Season, SeasonNumber = 1, ParentMediaCollection = series };
            var unknownSeason = new MediaCollection { Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollection = series };
            db.MediaCollections.AddRange(numberedSeason, unknownSeason);
            await db.SaveChangesAsync();
            var numberedEpisode = new MediaEntity { Title = "Visible numbered episode", MediaType = TvShow, MediaCollectionId = numberedSeason.Id, EpisodeNumber = 1 };
            var unknownEpisode = new MediaEntity { Title = "Hidden unknown episode", MediaType = TvShow, MediaCollectionId = unknownSeason.Id };
            db.Media.AddRange(numberedEpisode, unknownEpisode);
            await db.SaveChangesAsync();
            numberedEpisodeId = numberedEpisode.Id;
        }

        var response = await Client.GetAsync("/api/Reviews/unreviewedByType?mediaType=TvShow&pageSize=50&includeTotalCount=true");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();
        result!.Items.Should().Contain(item => item.Id == numberedEpisodeId);
        result.Items.Should().NotContain(item => item.Title == "Hidden unknown episode");
    }

    [Fact]
    public async Task InsertRejectsHiddenSeriesAndUnknownEpisodeButAcceptsSeriesWithoutSeasons()
    {
        long hiddenSeriesId, unknownEpisodeId, manualSeriesId, templateId, fieldId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var template = await db.Templates.Include(item => item.Fields).FirstAsync(item => item.MediaType == TvShow);
            templateId = template.Id;
            fieldId = template.Fields.First().Id;
            var hidden = new MediaCollection { Title = "Hidden review target", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            var unknown = new MediaCollection { Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollection = hidden };
            var manual = new MediaCollection { Title = "Manual review target", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            var episode = new MediaEntity { Title = "Unknown review target", MediaType = TvShow, MediaCollection = unknown };
            db.MediaCollections.AddRange(unknown, manual);
            db.Media.Add(episode);
            await db.SaveChangesAsync();
            hiddenSeriesId = hidden.Id;
            unknownEpisodeId = episode.Id;
            manualSeriesId = manual.Id;
        }

        (await PostInvalidAsync(NewRequest(templateId, fieldId, mediaCollectionId: hiddenSeriesId))).Type.Should().Contain("review_insert_validation_error");
        (await PostInvalidAsync(NewRequest(templateId, fieldId, mediaId: unknownEpisodeId))).Type.Should().Contain("review_insert_validation_error");
        (await CreateReviewAsync(NewRequest(templateId, fieldId, mediaCollectionId: manualSeriesId))).Kind.Should().Be("Series");
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        (await verifyDb.Reviews.AnyAsync(item => item.MediaCollectionId == hiddenSeriesId || item.MediaId == unknownEpisodeId)).Should().BeFalse();
    }

    private async Task<ReviewDto> CreateReviewAsync(ReviewInsertRequest request)
    {
        var response = await Client.PostAsJsonAsync(Api, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    private async Task<ProblemDetails> PostInvalidAsync(ReviewInsertRequest request)
    {
        var response = await Client.PostAsJsonAsync(Api, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
    }

    private static ReviewInsertRequest NewRequest(long templateId, long fieldId, long? mediaId = null, long? mediaCollectionId = null)
        => new()
        {
            MediaId = mediaId,
            MediaCollectionId = mediaCollectionId,
            TemplateId = templateId,
            Fields = [new ReviewFieldInsertRequest { TemplateFieldId = fieldId, Value = 7 }]
        };

    private static Review NewStoredReview(string userId, long templateId, long fieldId, long? mediaId = null, long? mediaCollectionId = null)
        => new()
        {
            UserId = userId,
            TemplateId = templateId,
            MediaId = mediaId,
            MediaCollectionId = mediaCollectionId,
            OverallScore = 7,
            Fields = [new ReviewField { TemplateFieldId = fieldId, Value = 7 }]
        };
}
