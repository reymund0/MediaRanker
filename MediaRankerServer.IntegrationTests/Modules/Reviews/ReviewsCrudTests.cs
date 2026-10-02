using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.IntegrationTests.Utils;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Reviews.Services;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Reviews;

public class ReviewsCrudTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture) 
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    const string basePath = "/api/Reviews";
    private Review _testReviews = null!;
    private MediaEntity _testMedia = null!;
    private MediaEntity _testUnreviewedMedia = null!;
    private Template _testTemplate = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Create a test Media. We can use the seeded MediaType, Template, and TemplateFields.
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            _testTemplate = dbContext.Templates.Include(t => t.Fields).First();

            var reviewedMedia = new MediaEntity
            {
                Title = "Test Media",
                MediaType = _testTemplate.MediaType,
                ReleaseDate = new DateOnly(2024, 1, 1),
            };
            dbContext.Media.Add(reviewedMedia);
            dbContext.SaveChanges();

            var review = new Review
            {
                UserId = TestAuthHandler.DefaultUserId,
                TemplateId = _testTemplate.Id,
                OverallScore = 5,
                MediaId = reviewedMedia.Id,
                Fields = [new ReviewField
                {
                    TemplateFieldId = _testTemplate.Fields.First().Id,
                    Value = 5
                }]
            };
            dbContext.Reviews.Add(review);

            var unreviewedMedia = new MediaEntity
            {
                Title = "Unreviewed Media",
                MediaType = _testTemplate.MediaType,
                ReleaseDate = new DateOnly(2024, 1, 1),
            };
            dbContext.Media.Add(unreviewedMedia);
            
            dbContext.SaveChanges();
            _testReviews = review;
            _testMedia = reviewedMedia;
            _testUnreviewedMedia = unreviewedMedia;
        }
    }
    
    [Fact]
    public async Task GetReviewsByMediaType_ReturnsExistingRows()
    {
        var response = await Client.GetAsync($"{basePath}/byMediaType/{_testMedia.MediaType}");
        TestUtils.AssertSuccessResponse(response);

        var Reviews = await response.Content.ReadFromJsonAsync<List<ReviewDto>>();
        Reviews.Should().NotBeNull();
        Reviews.Should().NotBeEmpty();
        Reviews.Should().Contain(r => r.Id == _testReviews.Id);
        var review = Reviews.Single(r => r.Id == _testReviews.Id);
        review.MediaCoverImageUrl.Should().BeNull();
        review.CoverStatus.Should().Be("unsupported");
        review.MediaReleaseDate.Should().Be(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public async Task GetReviewsByMediaType_OrdersByScoreRecencyThenId()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var olderHighScoreMedia = new MediaEntity { Title = "Ranking old high score", MediaType = _testMedia.MediaType };
        var firstRecentTieMedia = new MediaEntity { Title = "Ranking first recent tie", MediaType = _testMedia.MediaType };
        var secondRecentTieMedia = new MediaEntity { Title = "Ranking second recent tie", MediaType = _testMedia.MediaType };
        var olderTieMedia = new MediaEntity { Title = "Ranking older tie", MediaType = _testMedia.MediaType };
        db.Media.AddRange(olderHighScoreMedia, firstRecentTieMedia, secondRecentTieMedia, olderTieMedia);
        await db.SaveChangesAsync();

        var tieTimestamp = new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var olderHighScore = new Review
        {
            UserId = TestAuthHandler.DefaultUserId,
            TemplateId = _testTemplate.Id,
            MediaId = olderHighScoreMedia.Id,
            OverallScore = 10,
            UpdatedAt = tieTimestamp.AddDays(-5)
        };
        var firstRecentTie = new Review
        {
            UserId = TestAuthHandler.DefaultUserId,
            TemplateId = _testTemplate.Id,
            MediaId = firstRecentTieMedia.Id,
            OverallScore = 8,
            UpdatedAt = tieTimestamp
        };
        var secondRecentTie = new Review
        {
            UserId = TestAuthHandler.DefaultUserId,
            TemplateId = _testTemplate.Id,
            MediaId = secondRecentTieMedia.Id,
            OverallScore = 8,
            UpdatedAt = tieTimestamp
        };
        var olderTie = new Review
        {
            UserId = TestAuthHandler.DefaultUserId,
            TemplateId = _testTemplate.Id,
            MediaId = olderTieMedia.Id,
            OverallScore = 8,
            UpdatedAt = tieTimestamp.AddDays(-1)
        };
        db.Reviews.AddRange(olderHighScore, firstRecentTie, secondRecentTie, olderTie);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"{basePath}/byMediaType/{_testMedia.MediaType}");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<List<ReviewDto>>();

        result!.Select(review => review.Id).Should().ContainInOrder(
            olderHighScore.Id,
            firstRecentTie.Id,
            secondRecentTie.Id,
            olderTie.Id,
            _testReviews.Id);
    }

    [Fact]
    public async Task GetUnreviewedMedia_ReturnsUnreviewedMedia()
    {
        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaType={_testUnreviewedMedia.MediaType}&includeTotalCount=true");
        TestUtils.AssertSuccessResponse(response);

        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().Contain(m => m.Id == _testUnreviewedMedia.Id);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        var media = result.Items.Single(m => m.Id == _testUnreviewedMedia.Id);
        media.CoverImageUrl.Should().BeNull();
        media.CoverStatus.Should().Be("unsupported");
    }

    [Fact]
    public async Task GetUnreviewedMedia_Paging_SortSearchAndPageWork()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.AddRange(
            new MediaEntity { Title = "PagingTestAlpha", MediaType = _testTemplate.MediaType, ReleaseDate = new DateOnly(2020, 1, 1) },
            new MediaEntity { Title = "PagingTestBeta",  MediaType = _testTemplate.MediaType, ReleaseDate = new DateOnly(2021, 1, 1) }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaType={_testTemplate.MediaType}&searchField=title&searchTerm=PagingTest&sortField=releaseDate&sortDirection=desc&page=0&pageSize=1&includeTotalCount=true");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        result!.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(2);
        result.Items.First().Title.Should().Be("PagingTestAlpha");
    }

    [Fact]
    public async Task GetUnreviewedMedia_SearchOrdersPrefixMatchesBeforeContainsMatchesThenByTitle()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.AddRange(
            new MediaEntity { Title = "RankNeedle", MediaType = _testMedia.MediaType },
            new MediaEntity { Title = "RankNeedle 0 Long Prefix", MediaType = _testMedia.MediaType },
            new MediaEntity { Title = "RankNeedle A", MediaType = _testMedia.MediaType },
            new MediaEntity { Title = "RankNeedle Longer Title", MediaType = _testMedia.MediaType },
            new MediaEntity { Title = "A RankNeedle Match", MediaType = _testMedia.MediaType }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync(
            $"{basePath}/unreviewedByType?mediaType={_testMedia.MediaType}&searchField=title&searchTerm=rankneedle&sortField=releaseDate&sortDirection=desc&pageSize=10");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        result!.Items.Select(item => item.Title).Should().Equal(
            "RankNeedle",
            "RankNeedle 0 Long Prefix",
            "RankNeedle A",
            "RankNeedle Longer Title",
            "A RankNeedle Match");
    }

    [Fact]
    public async Task CreateReviews_CreatesNewRecord()
    {
        // Remove the existing ranked media to avoid duplicate review violation.
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        dbContext.Reviews.Remove(_testReviews);
        dbContext.SaveChanges();
        
        var request = new ReviewInsertRequest
        {
            MediaId = _testMedia.Id,
            TemplateId = _testTemplate.Id,
            Notes = "Test notes",
            ConsumedAt = DateTime.UtcNow,
            Fields = [new ReviewFieldInsertRequest
            {
                TemplateFieldId = _testTemplate.Fields.First().Id,
                Value = 5
            }]
        };
        var response = await Client.PostAsJsonAsync(basePath, request);
        TestUtils.AssertSuccessResponse(response);
        
        var Reviews = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Reviews.Should().NotBeNull();
        Reviews!.Id.Should().NotBe(_testReviews.Id);
    }

    [Fact]
    public async Task CreateReview_WhenMediaTypeIsIncompatible_DoesNotRequestArtwork()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PostgreSQLContext>();
        var incompatibleTemplate = new Template
        {
            Name = "TV review template",
            MediaType = "TvShow",
            UserId = TestAuthHandler.DefaultUserId,
            Fields = [new TemplateField { Name = "Story", Position = 1 }]
        };
        var movie = new MediaEntity
        {
            Title = "Movie rejected by TV template",
            MediaType = "Movie",
            ExternalSource = MediaExternalSource.Imdb,
            ExternalId = "tt0133093"
        };
        db.AddRange(incompatibleTemplate, movie);
        await db.SaveChangesAsync();

        var artwork = new TrackingArtworkService();
        var mediaService = new MediaService(
            db,
            artwork,
            services.GetRequiredService<IValidator<MediaUpsertRequest>>(),
            services.GetRequiredService<IPublisher>());
        var reviewService = new ReviewService(
            db,
            services.GetRequiredService<IValidator<ReviewInsertRequest>>(),
            services.GetRequiredService<IValidator<ReviewUpdateRequest>>(),
            mediaService,
            services.GetRequiredService<ITemplateService>(),
            artwork);

        var act = () => reviewService.CreateReviewAsync(TestAuthHandler.DefaultUserId, new ReviewInsertRequest
        {
            MediaId = movie.Id,
            TemplateId = incompatibleTemplate.Id,
            Fields = [new ReviewFieldInsertRequest { TemplateFieldId = incompatibleTemplate.Fields.Single().Id, Value = 5 }]
        });

        await act.Should().ThrowAsync<DomainException>()
            .Where(exception => exception.Type == "review_media_type_mismatch");
        artwork.MediaRequests.Should().BeEmpty();
        (await db.MediaCovers.CountAsync()).Should().Be(0);
    }
    
    [Fact]
    public async Task UpdateReviews_UpdatesExistingRecord()
    {
        var request = new ReviewUpdateRequest
        {
            Id = _testReviews.Id,
            Notes = "Updated notes",
            ConsumedAt = DateTime.UtcNow,
            Fields = [new ReviewFieldUpdateRequest
            {
                TemplateFieldId = _testTemplate.Fields.First().Id,
                Value = 10
            }]
        };
        var response = await Client.PatchAsJsonAsync($"{basePath}/update", request);
        TestUtils.AssertSuccessResponse(response);
        
        var Reviews = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Reviews.Should().NotBeNull();
        Reviews!.Id.Should().Be(_testReviews.Id);
        Reviews.Notes.Should().Be("Updated notes");
    }
    
    [Fact]
    public async Task DeleteReviews_DeletesRecord()
    {
        var response = await Client.DeleteAsync($"{basePath}/{_testReviews.Id}");
        TestUtils.AssertSuccessResponse(response);
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var exists = await db.Reviews.AnyAsync(r => r.Id == _testReviews.Id);
        exists.Should().BeFalse();
    }

    private sealed class TrackingArtworkService : IArtworkService
    {
        public List<long> MediaRequests { get; } = [];

        public Task<IReadOnlyDictionary<long, CoverPresentation>> GetMediaArtworkAsync(IEnumerable<long> mediaIds, CancellationToken ct = default)
        {
            MediaRequests.AddRange(mediaIds);
            return Task.FromResult<IReadOnlyDictionary<long, CoverPresentation>>(new Dictionary<long, CoverPresentation>());
        }

        public Task<IReadOnlyDictionary<long, CoverPresentation>> GetCollectionArtworkAsync(IEnumerable<long> collectionIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<long, CoverPresentation>>(new Dictionary<long, CoverPresentation>());
    }
}
