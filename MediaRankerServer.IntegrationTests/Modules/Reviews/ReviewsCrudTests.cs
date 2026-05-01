using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.IntegrationTests.Utils;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;
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
                MediaTypeId = _testTemplate.MediaTypeId,
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
                MediaTypeId = _testTemplate.MediaTypeId,
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
        var response = await Client.GetAsync($"{basePath}/byMediaType/{_testMedia.MediaTypeId}");
        TestUtils.AssertSuccessResponse(response);

        var Reviews = await response.Content.ReadFromJsonAsync<List<ReviewDto>>();
        Reviews.Should().NotBeNull();
        Reviews.Should().NotBeEmpty();
        Reviews.Should().Contain(r => r.Id == _testReviews.Id);
    }

    [Fact]
    public async Task GetUnreviewedMedia_ReturnsUnreviewedMedia()
    {
        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testUnreviewedMedia.MediaTypeId}");
        TestUtils.AssertSuccessResponse(response);

        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().Contain(m => m.Id == _testUnreviewedMedia.Id);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetUnreviewedMedia_DefaultPaging_ReturnsAtMost25Items()
    {
        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testUnreviewedMedia.MediaTypeId}");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        result!.Items.Count.Should().BeLessThanOrEqualTo(25);
        result.Page.Should().Be(0);
        result.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task GetUnreviewedMedia_PastEndPage_ReturnsEmptyItemsAndCorrectTotalCount()
    {
        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testUnreviewedMedia.MediaTypeId}&page=9999&pageSize=25");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetUnreviewedMedia_SortByReleaseDateDesc_NullsLast()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.Add(new MediaEntity { Title = "Dated Unreviewed", MediaTypeId = _testTemplate.MediaTypeId, ReleaseDate = new DateOnly(2018, 6, 1) });
        db.Media.Add(new MediaEntity { Title = "NullDate Unreviewed", MediaTypeId = _testTemplate.MediaTypeId, ReleaseDate = null });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testTemplate.MediaTypeId}&sortField=releaseDate&sortDirection=desc&pageSize=100");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        var items = result!.Items;
        var nullDateIndex = items.ToList().FindIndex(m => m.ReleaseDate == null);
        nullDateIndex.Should().BeGreaterThan(-1);
        items.Take(nullDateIndex).Should().OnlyContain(m => m.ReleaseDate != null);
    }

    [Fact]
    public async Task GetUnreviewedMedia_TitleTiebreaker_DeterministicIdAscOrder()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var a = new MediaEntity { Title = "Unreviewed Tie", MediaTypeId = _testTemplate.MediaTypeId };
        var b = new MediaEntity { Title = "Unreviewed Tie", MediaTypeId = _testTemplate.MediaTypeId };
        db.Media.AddRange(a, b);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testTemplate.MediaTypeId}&sortField=title&sortDirection=asc&pageSize=100");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        var ties = result!.Items.Where(m => m.Title == "Unreviewed Tie").ToList();
        ties.Count.Should().Be(2);
        ties[0].Id.Should().BeLessThan(ties[1].Id);
    }

    [Fact]
    public async Task GetUnreviewedMedia_InvalidSortField_ReturnsPagingValidationErrorProblemType()
    {
        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testUnreviewedMedia.MediaTypeId}&sortField=notAllowedForThisEndpoint");

        response.IsSuccessStatusCode.Should().BeFalse();
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Be("paging_validation_error");
    }

    [Fact]
    public async Task GetUnreviewedMedia_SearchByTitle_FiltersResults()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.Add(new MediaEntity { Title = "UniqueUnreviewedAbc", MediaTypeId = _testTemplate.MediaTypeId });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync($"{basePath}/unreviewedByType?mediaTypeId={_testTemplate.MediaTypeId}&searchField=title&searchTerm=UniqueUnreviewedAbc");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<UnreviewedMediaDto>>();

        result!.Items.Should().OnlyContain(m => m.Title.Contains("UniqueUnreviewedAbc"));
        result.TotalCount.Should().Be(1);
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
}
