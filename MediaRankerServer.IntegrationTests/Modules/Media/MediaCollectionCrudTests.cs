using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.IntegrationTests.Utils;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaCollectionCrudTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    // Seeded system IDs
    private const long MovieTypeId = -3;

    private MediaCollection _testSeries = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        _testSeries = new MediaCollection
        {
            Title = "Test Series",
            CollectionType = MediaCollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 1, 1),
        };
        db.MediaCollections.Add(_testSeries);
        db.SaveChanges();
    }

    [Fact]
    public async Task GetCollections_ReturnsExistingRows()
    {
        var response = await Client.GetAsync("/api/mediacollection");

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaCollectionDto>>();

        result.Should().NotBeNull();
        result!.Items.Should().Contain(c => c.Title == _testSeries.Title);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetCollections_Paging_SortSearchAndPageWork()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.MediaCollections.AddRange(
            new MediaCollection { Title = "PagingTestAlpha", CollectionType = MediaCollectionType.Series, MediaTypeId = MovieTypeId, ReleaseDate = new DateOnly(2020, 1, 1) },
            new MediaCollection { Title = "PagingTestBeta",  CollectionType = MediaCollectionType.Series, MediaTypeId = MovieTypeId, ReleaseDate = new DateOnly(2021, 1, 1) }
        );
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/mediacollection?searchField=title&searchTerm=PagingTest&sortField=releaseDate&sortDirection=desc&page=0&pageSize=1");
        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<PageResult<MediaCollectionDto>>();

        result!.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(2);
        result.Items.First().Title.Should().Be("PagingTestBeta");
    }

    [Fact]
    public async Task UpsertCollection_Create_PersistsCollection()
    {
        var request = new MediaCollectionUpsertRequest
        {
            Title = "New Movie Series",
            CollectionType = MediaCollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2021, 5, 1),
        };

        var response = await Client.PostAsJsonAsync("/api/mediacollection", request);

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<MediaCollectionDto>();

        result.Should().NotBeNull();
        result!.Title.Should().Be("New Movie Series");
        result.CollectionType.Should().Be("Series");
        result.MediaTypeId.Should().Be(MovieTypeId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var entity = await db.MediaCollections.FirstOrDefaultAsync(c => c.Id == result.Id);
        entity.Should().NotBeNull();
    }

    [Fact]
    public async Task UpsertCollection_Update_PersistsChanges()
    {
        var request = new MediaCollectionUpsertRequest
        {
            Id = _testSeries.Id,
            Title = "Updated Series Title",
            CollectionType = MediaCollectionType.Series,
            MediaTypeId = MovieTypeId,
            ReleaseDate = new DateOnly(2020, 6, 15),
        };

        var response = await Client.PostAsJsonAsync("/api/mediacollection", request);

        TestUtils.AssertSuccessResponse(response);
        var result = await response.Content.ReadFromJsonAsync<MediaCollectionDto>();
        result!.Title.Should().Be("Updated Series Title");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var entity = await db.MediaCollections.FirstOrDefaultAsync(c => c.Id == _testSeries.Id);
        entity!.Title.Should().Be("Updated Series Title");
        entity.ReleaseDate.Should().Be(new DateOnly(2020, 6, 15));
    }

    [Fact]
    public async Task DeleteCollection_RemovesRow()
    {
        var response = await Client.DeleteAsync($"/api/mediacollection/{_testSeries.Id}");

        TestUtils.AssertSuccessResponse(response);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var entity = await db.MediaCollections.FirstOrDefaultAsync(c => c.Id == _testSeries.Id);
        entity.Should().BeNull();
    }
}
