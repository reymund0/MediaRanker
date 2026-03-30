using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.IntegrationTests.Utils;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Shared.Data;
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
            var review = new Review
            {
                UserId = TestAuthHandler.DefaultUserId,
                TemplateId = _testTemplate.Id,
                OverallScore = 5,
                Media = new MediaEntity
                {
                    Title = "Test Media",
                    MediaTypeId = _testTemplate.MediaTypeId,
                    ReleaseDate = new DateOnly(2024, 1, 1),
                },
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
            _testMedia = review.Media;
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

        var unreviewedMedia = await response.Content.ReadFromJsonAsync<List<UnreviewedMediaDto>>();
        unreviewedMedia.Should().NotBeNull();
        unreviewedMedia.Should().NotBeEmpty();
        unreviewedMedia.Should().Contain(m => m.Id == _testUnreviewedMedia.Id);
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
