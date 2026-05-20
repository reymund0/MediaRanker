using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaTypesTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture) 
    : IntegrationTestBase(postgresFixture, localStackFixture)
{

    [Fact]
    public async Task GetMediaTypes_ReturnsSeededMediaTypes()
    {
        // Act
        var response = await Client.GetAsync("/api/mediatypes");

        // Assert
        response.EnsureSuccessStatusCode();
        var mediaTypes = await response.Content.ReadFromJsonAsync<List<MediaTypeResponse>>();
        
        mediaTypes.Should().NotBeNull();
        mediaTypes.Should().NotBeEmpty();
        mediaTypes.Should().Contain(m => m.Name == "Movie");
        mediaTypes.Should().Contain(m => m.Name == "TV Show");
        mediaTypes.Should().Contain(m => m.Name == "Video Game");
        mediaTypes.Should().Contain(m => m.Name == "Book");
    }

    [Fact]
    public async Task GetMediaTypes_ReturnsDescendingById()
    {
        var response = await Client.GetAsync("/api/mediatypes");

        response.EnsureSuccessStatusCode();
        var mediaTypes = await response.Content.ReadFromJsonAsync<List<MediaTypeResponse>>();

        mediaTypes.Should().NotBeNull();
        var ids = mediaTypes!.Select(m => m.Id).ToList();
        ids.Should().BeInDescendingOrder();
        ids.Should().Equal([-1, -2, -3, -4, -5, -6]);
    }

    private record MediaTypeResponse(int Id, string Name);
}
