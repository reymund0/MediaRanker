using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using Xunit;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaTypesTests : IntegrationTestBase
{
    public MediaTypesTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

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

    private record MediaTypeResponse(int Id, string Name);
}
