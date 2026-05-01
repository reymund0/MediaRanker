using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.IntegrationTests.Utils;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaCrudTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture) 
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const long MovieMediaTypeId = -3;

    private MediaEntity _testMedia = null!;
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Create a test Media
        using var scope = Factory.Services.CreateScope();
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var media = new MediaEntity
            {
                Title = "Test Media",
                MediaTypeId = MovieMediaTypeId,
                ReleaseDate = new DateOnly(2024, 1, 1),
            };
            dbContext.Media.Add(media);
            dbContext.SaveChanges();
            _testMedia = media;
        }
    }
    
    [Fact]
    public async Task GetMedia_ReturnsExistingRows()
    {
        var response = await Client.GetAsync("/api/media");

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result.Should().NotBeNull();
        result!.Items.Should().Contain(m => m.Title == _testMedia.Title);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetMedia_DefaultPaging_ReturnsAtMost25Items()
    {
        var response = await Client.GetAsync("/api/media");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result!.Items.Count.Should().BeLessThanOrEqualTo(25);
        result.PageSize.Should().Be(25);
        result.Page.Should().Be(0);
    }

    [Fact]
    public async Task GetMedia_ExplicitPageAndSize_ReturnsCorrectSlice()
    {
        var response = await Client.GetAsync("/api/media?page=0&pageSize=1");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result!.Items.Count.Should().Be(1);
        result.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task GetMedia_PastEndPage_ReturnsEmptyItemsAndCorrectTotalCount()
    {
        var response = await Client.GetAsync("/api/media?page=9999&pageSize=25");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetMedia_SortByReleaseDateDesc_NullsLast()
    {
        // Seed one with null ReleaseDate
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.Add(new MediaEntity { Title = "NullDate Media", MediaTypeId = MovieMediaTypeId, ReleaseDate = null });
        db.Media.Add(new MediaEntity { Title = "Dated Media", MediaTypeId = MovieMediaTypeId, ReleaseDate = new DateOnly(2020, 1, 1) });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/media?sortField=releaseDate&sortDirection=desc&pageSize=100");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        var items = result!.Items;
        var nullDateIndex = items.ToList().FindIndex(m => m.ReleaseDate == null);
        nullDateIndex.Should().BeGreaterThan(-1);
        // All non-null dates should come before nulls
        items.Take(nullDateIndex).Should().OnlyContain(m => m.ReleaseDate != null);
    }

    [Fact]
    public async Task GetMedia_TitleTiebreaker_DeterministicIdAscOrder()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var a = new MediaEntity { Title = "Tie Title", MediaTypeId = MovieMediaTypeId };
        var b = new MediaEntity { Title = "Tie Title", MediaTypeId = MovieMediaTypeId };
        db.Media.AddRange(a, b);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/media?sortField=title&sortDirection=asc&pageSize=100");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        var ties = result!.Items.Where(m => m.Title == "Tie Title").ToList();
        ties.Count.Should().Be(2);
        ties[0].Id.Should().BeLessThan(ties[1].Id);
    }

    [Fact]
    public async Task GetMedia_InvalidSortField_ReturnsPagingValidationErrorProblemType()
    {
        var response = await Client.GetAsync("/api/media?sortField=notAllowedForThisEndpoint");

        response.IsSuccessStatusCode.Should().BeFalse();
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Be("paging_validation_error");
    }

    [Fact]
    public async Task GetMedia_SearchByTitle_FiltersResults()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.Add(new MediaEntity { Title = "UniqueSearchableXyz", MediaTypeId = MovieMediaTypeId });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/media?searchField=title&searchTerm=UniqueSearchableXyz");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result!.Items.Should().OnlyContain(m => m.Title.Contains("UniqueSearchableXyz"));
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMedia_MalformedPage_DefaultBindingError_400()
    {
        var response = await Client.GetAsync("/api/media?page=abc");
        response.IsSuccessStatusCode.Should().BeFalse();
        ((int)response.StatusCode).Should().Be(400);
    }

    [Fact]
    public async Task UpsertMedia_Create_PersistsMedia()
    {
        var request = new MediaUpsertRequest
        {
            Title = "Arrival",
            MediaTypeId = MovieMediaTypeId,
            ReleaseDate = new DateOnly(2016, 11, 11),
        };

        var response = await Client.PostAsJsonAsync("/api/media", request);

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<MediaDto>();

        result.Should().NotBeNull();
        result!.Title.Should().Be("Arrival");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var dbMedia = await db.Media.FirstOrDefaultAsync(m => m.Id == result.Id);

        dbMedia.Should().NotBeNull();
        dbMedia!.Title.Should().Be("Arrival");
    }

    [Fact]
    public async Task UpsertMedia_Update_PersistsChanges()
    {
        var request = new MediaUpsertRequest
        {
            Id = _testMedia.Id,
            Title = "Blade Runner: Final Cut",
            MediaTypeId = MovieMediaTypeId,
            ReleaseDate = new DateOnly(1982, 6, 25),
        };

        var response = await Client.PostAsJsonAsync("/api/media", request);

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<MediaDto>();

        result.Should().NotBeNull();
        result!.Title.Should().Be("Blade Runner: Final Cut");

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var dbMedia = await verifyDb.Media.FirstOrDefaultAsync(m => m.Id == _testMedia.Id);

        dbMedia.Should().NotBeNull();
        dbMedia!.Title.Should().Be("Blade Runner: Final Cut");
    }

    [Fact]
    public async Task DeleteMedia_RemovesExistingRow()
    {
        var response = await Client.DeleteAsync($"/api/media/{_testMedia.Id}");

        TestUtils.AssertSuccessResponse(response);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var dbMedia = await verifyDb.Media.FirstOrDefaultAsync(m => m.Id == _testMedia.Id);
        dbMedia.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMedia_NotFound_ReturnsMediaNotFoundProblemType()
    {
        var response = await Client.DeleteAsync("/api/media/999999");

        response.IsSuccessStatusCode.Should().BeFalse();

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Type.Should().Be("media_not_found");
    }

    [Fact]
    public async Task UpsertMedia_CreateWithCover_PersistsMediaAndCopiesMetadata()
    {
        // 1. Seed a MediaCover for the test user
        long mediaCoverId;
        long fileUploadId = 10;
        var fileKey = "covers/test-cover.png";
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var upload = new MediaCover
            {
                FileKey = fileKey,
                FileUploadId = fileUploadId,
                FileName = "test-cover.png",
                FileContentType = "image/png",
                FileSizeBytes = 1024,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.MediaCovers.Add(upload);
            await db.SaveChangesAsync();
            mediaCoverId = upload.Id;
        }

        // 2. Perform the Create Upsert with the CoverUploadId
        var request = new MediaUpsertRequest
        {
            Title = "Media With Cover",
            MediaTypeId = MovieMediaTypeId,
            ReleaseDate = new DateOnly(2024, 1, 1),
            CoverUploadId = fileUploadId
        };

        var response = await Client.PostAsJsonAsync("/api/media", request);
        TestUtils.AssertSuccessResponse(response);
        
        var result = await response.Content.ReadFromJsonAsync<MediaDto>();
        result.Should().NotBeNull();
        result!.Title.Should().Be("Media With Cover");
        result.CoverImageUrl.Should().NotBeNullOrEmpty();

        // 3. Verify Media entity has the metadata and FileUpload is now "Copied"
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var dbMedia = await db.Media.FirstOrDefaultAsync(m => m.Id == result.Id);
            dbMedia.Should().NotBeNull();
            dbMedia.CoverId.Should().Be(mediaCoverId);
        }
    }
}
