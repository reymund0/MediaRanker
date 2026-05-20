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
        var response = await Client.GetAsync("/api/media?mediaTypeId=-3&includeTotalCount=true");

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result.Should().NotBeNull();
        result!.Items.Should().Contain(m => m.Title == _testMedia.Title);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetMedia_Paging_SortSearchAndPageWork()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.AddRange(
            new MediaEntity { Title = "PagingTestAlpha", MediaTypeId = MovieMediaTypeId, ReleaseDate = new DateOnly(2020, 1, 1) },
            new MediaEntity { Title = "PagingTestBeta",  MediaTypeId = MovieMediaTypeId, ReleaseDate = new DateOnly(2021, 1, 1) }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/media?mediaTypeId=-3&searchField=title&searchTerm=PagingTest&sortField=releaseDate&sortDirection=desc&page=0&pageSize=1&includeTotalCount=true");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();

        result!.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(2);
        result.Items[0].Title.Should().Be("PagingTestBeta");
    }


    [Fact]
    public async Task GetMedia_MissingMediaTypeId_Returns400WithMediaValidationError()
    {
        var response = await Client.GetAsync("/api/media");

        response.IsSuccessStatusCode.Should().BeFalse();
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Type.Should().Be("media_validation_error");
    }

    [Fact]
    public async Task GetMedia_UnknownMediaTypeId_Returns400WithMediaTypeNotFound()
    {
        var response = await Client.GetAsync("/api/media?mediaTypeId=999999");

        response.IsSuccessStatusCode.Should().BeFalse();
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Type.Should().Be("media_type_not_found");
    }

    [Fact]
    public async Task GetMedia_ScopedToMediaType_ReturnsOnlyMatchingRows()
    {
        const long bookMediaTypeId = -2;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Media.AddRange(
            new MediaEntity { Title = "ScopedMovie", MediaTypeId = MovieMediaTypeId, ReleaseDate = new DateOnly(2024, 1, 1) },
            new MediaEntity { Title = "ScopedBook", MediaTypeId = bookMediaTypeId, ReleaseDate = new DateOnly(2024, 1, 1) }
        );
        await db.SaveChangesAsync();

        var movieResponse = await Client.GetAsync($"/api/media?mediaTypeId={MovieMediaTypeId}&includeTotalCount=true");
        TestUtils.AssertSuccessResponse(movieResponse);
        var movieResult = await movieResponse.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        movieResult!.Items.Should().AllSatisfy(m => m.MediaTypeId.Should().Be(MovieMediaTypeId));
        movieResult.Items.Should().Contain(m => m.Title == "ScopedMovie");
        movieResult.Items.Should().NotContain(m => m.Title == "ScopedBook");

        var bookResponse = await Client.GetAsync($"/api/media?mediaTypeId={bookMediaTypeId}&includeTotalCount=true");
        TestUtils.AssertSuccessResponse(bookResponse);
        var bookResult = await bookResponse.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        bookResult!.Items.Should().AllSatisfy(m => m.MediaTypeId.Should().Be(bookMediaTypeId));
        bookResult.Items.Should().Contain(m => m.Title == "ScopedBook");
        bookResult.Items.Should().NotContain(m => m.Title == "ScopedMovie");
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
