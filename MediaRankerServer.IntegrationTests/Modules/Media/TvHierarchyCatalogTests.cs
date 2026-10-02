using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentValidation;
using MediatR;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class TvHierarchyCatalogTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const string TvShow = "TvShow";

    [Fact]
    public async Task SeriesSearch_ReturnsRelevantFilteredSeriesWithNumberedCountsAndYears()
    {
        long expectedId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var exact = Series("HierarchyNeedle", new DateOnly(2010, 1, 1));
            var prefixLong = Series("HierarchyNeedle Extended", new DateOnly(2011, 1, 1));
            var prefixShort = Series("HierarchyNeedle A", new DateOnly(2012, 1, 1));
            var contained = Series("A HierarchyNeedle Match", new DateOnly(2013, 1, 1));
            var otherType = Series("HierarchyNeedle Movie", new DateOnly(2014, 1, 1), "Movie");
            var noSeason = Series("HierarchyNeedle Hand Added", new DateOnly(2015, 1, 1));
            var allUnknown = Series("HierarchyNeedle Unknown", new DateOnly(2016, 1, 1));
            db.MediaCollections.AddRange(exact, prefixLong, prefixShort, contained, otherType, noSeason, allUnknown);
            await db.SaveChangesAsync();
            expectedId = exact.Id;
            db.MediaCollections.AddRange(
                Season(exact, 9, new DateOnly(2018, 1, 1)),
                Season(exact, 10, new DateOnly(2020, 1, 1)),
                Season(allUnknown, null, new DateOnly(2024, 1, 1)));
            await db.SaveChangesAsync();
            var seasons = await db.MediaCollections.Where(c => c.ParentMediaCollectionId == exact.Id).ToListAsync();
            db.Media.AddRange(
                new MediaEntity { Title = "Count episode 1", MediaType = TvShow, MediaCollectionId = seasons[0].Id },
                new MediaEntity { Title = "Count episode 2", MediaType = TvShow, MediaCollectionId = seasons[1].Id },
                new MediaEntity { Title = "Count episode 3", MediaType = TvShow, MediaCollectionId = seasons[1].Id });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/mediacollection?mediaType=TvShow&collectionType=Series&searchField=title&searchTerm=HierarchyNeedle&pageSize=20&includeTotalCount=true");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaCollectionDto>>();
        result!.Items.Select(item => item.Title).Should().Equal(
            "HierarchyNeedle", "HierarchyNeedle A", "HierarchyNeedle Extended",
            "HierarchyNeedle Hand Added", "A HierarchyNeedle Match");
        result.Items.Select(item => item.Title).Should().NotContain("HierarchyNeedle Movie", "only TV series should be returned");
        result.Items.Should().NotContain(item => item.Title == "HierarchyNeedle Unknown");
        result.Items.Should().Contain(item => item.Title == "HierarchyNeedle Hand Added");
        result.Items[0].Id.Should().Be(expectedId);
        var exactDto = result.Items.Single(item => item.Title == "HierarchyNeedle");
        exactDto.SeasonCount.Should().Be(2);
        exactDto.EpisodeCount.Should().Be(3);
        exactDto.StartYear.Should().Be(2010);
        exactDto.EndYear.Should().Be(2020);
        result.TotalCount.Should().Be(5);
        var deepLinkResponse = await Client.GetAsync($"/api/mediacollection/{expectedId}");
        deepLinkResponse.EnsureSuccessStatusCode();
        var deepLink = await deepLinkResponse.Content.ReadFromJsonAsync<MediaCollectionDto>();
        deepLink!.SeasonCount.Should().Be(2);
        deepLink.EpisodeCount.Should().Be(3);
        deepLink.EndYear.Should().Be(2020);

        static MediaCollection Series(string title, DateOnly releaseDate, string mediaType = TvShow) => new()
        {
            Title = title, CollectionType = MediaCollectionType.Series, MediaType = mediaType, ReleaseDate = releaseDate
        };
        static MediaCollection Season(MediaCollection series, int? number, DateOnly date) => new()
        {
            Title = number?.ToString() ?? "Unknown", CollectionType = MediaCollectionType.Season,
            MediaType = TvShow, ParentMediaCollection = series, SeasonNumber = number, ReleaseDate = date
        };
    }

    [Fact]
    public async Task SeasonList_IsNumericUnpagedAndOmitsUnknownSeason()
    {
        long seriesId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var series = new MediaCollection { Title = "Hierarchy Season Sort", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            db.MediaCollections.Add(series);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            db.MediaCollections.AddRange(
                new MediaCollection { Title = "0", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId, SeasonNumber = 0 },
                new MediaCollection { Title = "10", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId, SeasonNumber = 10 },
                new MediaCollection { Title = "9", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId, SeasonNumber = 9 },
                new MediaCollection { Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId });
            await db.SaveChangesAsync();
        }
        var response = await Client.GetAsync($"/api/mediacollection?mediaType=TvShow&collectionType=Season&parentId={seriesId}&pageSize=1");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaCollectionDto>>();
        result!.Items.Select(item => item.SeasonNumber).Should().Equal(0, 9, 10);
        result.PageSize.Should().Be(3);
        var hiddenResponse = await Client.GetAsync($"/api/mediacollection/{(await GetUnknownSeasonId(seriesId))}");
        hiddenResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Episodes_AreScopedSortedAndUnknownSeasonRowsAreHiddenFromListAndLookup()
    {
        long seriesId;
        long numberedSeasonId;
        long unknownSeasonId;
        long unknownEpisodeId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var series = new MediaCollection { Title = "Hierarchy Episode Sort", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            db.MediaCollections.Add(series);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            var numbered = new MediaCollection { Title = "1", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId, SeasonNumber = 1 };
            var unknown = new MediaCollection { Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId };
            db.MediaCollections.AddRange(numbered, unknown);
            await db.SaveChangesAsync();
            numberedSeasonId = numbered.Id;
            unknownSeasonId = unknown.Id;
            var episodes = new[]
            {
                new MediaEntity { Title = "Zebra tie", MediaType = TvShow, MediaCollectionId = numbered.Id, EpisodeNumber = 2 },
                new MediaEntity { Title = "Alpha tie", MediaType = TvShow, MediaCollectionId = numbered.Id, EpisodeNumber = 2 },
                new MediaEntity { Title = "First episode", MediaType = TvShow, MediaCollectionId = numbered.Id, EpisodeNumber = 1 },
                new MediaEntity { Title = "Unknown episode", MediaType = TvShow, MediaCollectionId = unknown.Id, EpisodeNumber = 1 },
                new MediaEntity { Title = "Unnumbered episode", MediaType = TvShow, MediaCollectionId = numbered.Id }
            };
            db.Media.AddRange(episodes);
            await db.SaveChangesAsync();
            unknownEpisodeId = episodes[3].Id;
        }

        var firstPageResponse = await Client.GetAsync($"/api/media?mediaType=TvShow&mediaCollectionId={numberedSeasonId}&sortField=episodeNumber&page=0&pageSize=2");
        firstPageResponse.EnsureSuccessStatusCode();
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        firstPage!.Items.Select(item => item.Title).Should().Equal("First episode", "Alpha tie");
        firstPage.Items.First().SeriesId.Should().Be(seriesId);
        firstPage.Items.First().SeriesTitle.Should().Be("Hierarchy Episode Sort");
        firstPage.Items.First().SeasonNumber.Should().Be(1);
        var secondPageResponse = await Client.GetAsync($"/api/media?mediaType=TvShow&mediaCollectionId={numberedSeasonId}&sortField=episodeNumber&page=1&pageSize=2");
        secondPageResponse.EnsureSuccessStatusCode();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        secondPage!.Items.Select(item => item.Title).Should().Equal("Zebra tie", "Unnumbered episode");

        var unknownSeasonMedia = await Client.GetAsync($"/api/media?mediaType=TvShow&mediaCollectionId={unknownSeasonId}&includeTotalCount=true");
        unknownSeasonMedia.EnsureSuccessStatusCode();
        (await unknownSeasonMedia.Content.ReadFromJsonAsync<PageResult<MediaDto>>())!.Items.Should().BeEmpty();
        using var verifyScope = Factory.Services.CreateScope();
        var mediaService = verifyScope.ServiceProvider.GetRequiredService<IMediaService>();
        (await mediaService.GetMediaByIdAsync(unknownEpisodeId, CancellationToken.None, requestArtwork: false)).Should().BeNull();
    }

    [Fact]
    public async Task SeriesRemovalCountsIncludeAllDescendantMediaAndSeriesReviews_AndDeletePublishesFullTreeIds()
    {
        long seriesId;
        long episodeId;
        long directMediaId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var tvTemplateId = await db.Templates.Where(template => template.MediaType == TvShow).Select(template => template.Id).FirstAsync();
            var series = new MediaCollection { Title = "Hierarchy Removal", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
            db.MediaCollections.Add(series);
            await db.SaveChangesAsync();
            seriesId = series.Id;
            var season = new MediaCollection { Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season, ParentMediaCollectionId = seriesId };
            var episode = new MediaEntity { Title = "Hidden episode", MediaType = TvShow };
            var direct = new MediaEntity { Title = "Legacy direct episode", MediaType = TvShow, MediaCollectionId = seriesId };
            db.MediaCollections.Add(season);
            db.Media.Add(direct);
            await db.SaveChangesAsync();
            episode.MediaCollectionId = season.Id;
            db.Media.Add(episode);
            await db.SaveChangesAsync();
            episodeId = episode.Id;
            directMediaId = direct.Id;
            db.Reviews.AddRange(
                new Review { UserId = "series-removal-episode", MediaId = episodeId, TemplateId = tvTemplateId, OverallScore = 8 },
                new Review { UserId = "series-removal-direct", MediaId = directMediaId, TemplateId = tvTemplateId, OverallScore = 7 },
                new Review { UserId = "series-removal-series", MediaCollectionId = seriesId, TemplateId = tvTemplateId, OverallScore = 9 });
            await db.SaveChangesAsync();
        }

        var countsResponse = await Client.GetAsync($"/api/mediacollection/{seriesId}/removal-counts");
        countsResponse.EnsureSuccessStatusCode();
        var counts = await countsResponse.Content.ReadFromJsonAsync<SeriesRemovalCountsDto>();
        counts!.EpisodeCount.Should().Be(2);
        counts.ReviewCount.Should().Be(3);

        using (var failedScope = Factory.Services.CreateScope())
        {
            var failedService = new MediaCollectionService(
                failedScope.ServiceProvider.GetRequiredService<PostgreSQLContext>(),
                failedScope.ServiceProvider.GetRequiredService<IArtworkService>(),
                failedScope.ServiceProvider.GetRequiredService<IValidator<MediaCollectionUpsertRequest>>(),
                new ThrowingPublisher());
            var act = () => failedService.DeleteCollectionAsync(seriesId);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("publish failed");
        }
        using (var rollbackScope = Factory.Services.CreateScope())
        {
            var rollbackDb = rollbackScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            (await rollbackDb.MediaCollections.AnyAsync(collection => collection.Id == seriesId)).Should().BeTrue();
            (await rollbackDb.MediaCollections.AnyAsync(collection => collection.ParentMediaCollectionId == seriesId)).Should().BeTrue();
            (await rollbackDb.Media.AnyAsync(media => media.Id == episodeId || media.Id == directMediaId)).Should().BeTrue();
        }

        using var deleteScope = Factory.Services.CreateScope();
        var dbContext = deleteScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var recorder = new RecordingPublisher();
        var service = new MediaCollectionService(
            dbContext,
            deleteScope.ServiceProvider.GetRequiredService<IArtworkService>(),
            deleteScope.ServiceProvider.GetRequiredService<IValidator<MediaCollectionUpsertRequest>>(),
            recorder);
        await service.DeleteCollectionAsync(seriesId);
        recorder.Notification.Should().BeOfType<SeriesDeletedEvent>()
            .Which.EpisodeMediaIds.Should().BeEquivalentTo([episodeId, directMediaId]);
        (await dbContext.MediaCollections.AnyAsync(collection => collection.Id == seriesId)).Should().BeFalse();
        (await dbContext.MediaCollections.AnyAsync(collection => collection.ParentMediaCollectionId == seriesId)).Should().BeFalse();
        (await dbContext.Media.AnyAsync(media => media.Id == episodeId || media.Id == directMediaId)).Should().BeFalse();
    }

    [Fact]
    public async Task ManualSeasonCreateIsRejectedWithoutHidingItsSeries()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = new MediaCollection { Title = "Manual season parent", MediaType = TvShow, CollectionType = MediaCollectionType.Series };
        db.MediaCollections.Add(series);
        await db.SaveChangesAsync();

        var response = await Client.PostAsJsonAsync("/api/mediacollection", new MediaCollectionUpsertRequest
        {
            Title = "1", MediaType = TvShow, CollectionType = MediaCollectionType.Season,
            ParentMediaCollectionId = series.Id, ReleaseDate = new DateOnly(2020, 1, 1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("collection_manual_season_unsupported");
        (await db.MediaCollections.AnyAsync(c => c.ParentMediaCollectionId == series.Id)).Should().BeFalse();
        (await Client.GetAsync($"/api/mediacollection/{series.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HiddenSeriesUpdateIsRejectedBeforeChangingStoredDetails()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var series = new MediaCollection
        {
            Title = "Preserved hidden series", MediaType = TvShow, CollectionType = MediaCollectionType.Series,
            ReleaseDate = new DateOnly(2000, 1, 1)
        };
        db.MediaCollections.Add(new MediaCollection
        {
            Title = "Unknown", MediaType = TvShow, CollectionType = MediaCollectionType.Season,
            ParentMediaCollection = series
        });
        await db.SaveChangesAsync();

        var response = await Client.PostAsJsonAsync("/api/mediacollection", new MediaCollectionUpsertRequest
        {
            Id = series.Id, Title = "Should not persist", MediaType = TvShow,
            CollectionType = MediaCollectionType.Series, ReleaseDate = new DateOnly(2024, 1, 1)
        });

        response.IsSuccessStatusCode.Should().BeFalse();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("collection_not_found");
        var stored = await db.MediaCollections.AsNoTracking().SingleAsync(c => c.Id == series.Id);
        stored.Title.Should().Be("Preserved hidden series");
        stored.ReleaseDate.Should().Be(new DateOnly(2000, 1, 1));
    }

    private async Task<long> GetUnknownSeasonId(long seriesId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        return await db.MediaCollections.Where(collection => collection.ParentMediaCollectionId == seriesId && collection.SeasonNumber == null)
            .Select(collection => collection.Id).SingleAsync();
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public object? Notification { get; private set; }
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notification = notification;
            return Task.CompletedTask;
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notification = notification;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("publish failed"));
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.FromException(new InvalidOperationException("publish failed"));
    }
}
