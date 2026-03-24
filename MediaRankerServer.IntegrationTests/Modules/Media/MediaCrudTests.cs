using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.IntegrationTests.Utils;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaCrudTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture) 
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
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
                MediaTypeId = -3,
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
        var media = await response.Content.ReadFromJsonAsync<List<MediaDto>>();

        media.Should().NotBeNull();
        media.Should().Contain(m => m.Title == _testMedia.Title);
    }

    [Fact]
    public async Task UpsertMedia_Create_PersistsMedia()
    {
        var request = new MediaUpsertRequest
        {
            Title = "Arrival",
            MediaTypeId = -3,
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
            MediaTypeId = -3,
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
}
