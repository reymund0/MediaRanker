using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Rankings.Contracts;
using MediaRankerServer.Modules.Rankings.Entities;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Rankings;

public class MediaRankingsCrudTests(PostgresContainerFixture fixture) : IntegrationTestBase(fixture)
{
    const string basePath = "/api/RankedMedia";
    private RankedMedia _testRankedMedia = null!;
    private MediaEntity _testMedia = null!;
    private Template _testTemplate = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Create a test Media. We can use the seeded MediaType, Template, and TemplateFields.
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            _testTemplate = dbContext.Templates.Include(t => t.Fields).First();
            var rankedMedia = new RankedMedia
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
                Scores = [new RankedMediaScore
                {
                    TemplateFieldId = _testTemplate.Fields.First().Id,
                    Value = 5
                }]
            };
            dbContext.RankedMedia.Add(rankedMedia);
            dbContext.SaveChanges();
            _testRankedMedia = rankedMedia;
            _testMedia = rankedMedia.Media;
        }
    }
    
    [Fact]
    public async Task GetRankedMedia_ReturnsExistingRows()
    {
        var response = await Client.GetAsync(basePath);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to GET rankings. Status: {response.StatusCode}. Content: {content}");
        }
        response.EnsureSuccessStatusCode();

        var rankedMedia = await response.Content.ReadFromJsonAsync<List<RankedMediaDto>>();
        rankedMedia.Should().NotBeNull();
        rankedMedia.Should().NotBeEmpty();
        rankedMedia.Should().Contain(r => r.Id == _testRankedMedia.Id);
    }
    
    [Fact]
    public async Task CreateRankedMedia_CreatesNewRecord()
    {
        // Remove the existing ranked media to avoid duplicate review violation.
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        dbContext.RankedMedia.Remove(_testRankedMedia);
        dbContext.SaveChanges();
        
        var request = new RankedMediaUpsertRequest
        {
            UserId = TestAuthHandler.DefaultUserId,
            MediaId = _testMedia.Id,
            TemplateId = _testTemplate.Id,
            Notes = "Test notes",
            ConsumedAt = DateTime.UtcNow,
            Scores = [new RankedMediaScoreUpsertRequest
            {
                TemplateFieldId = _testTemplate.Fields.First().Id,
                Value = 5
            }]
        };
        var response = await Client.PostAsJsonAsync(basePath, request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to POST ranking. Status: {response.StatusCode}. Content: {content}");
        }
        response.EnsureSuccessStatusCode();
        
        var rankedMedia = await response.Content.ReadFromJsonAsync<RankedMediaDto>();
        rankedMedia.Should().NotBeNull();
        rankedMedia!.Id.Should().NotBe(_testRankedMedia.Id);
    }
    
    [Fact]
    public async Task UpdateRankedMedia_UpdatesExistingRecord()
    {
        var request = new RankedMediaUpsertRequest
        {
            Id = _testRankedMedia.Id,
            UserId = TestAuthHandler.DefaultUserId,
            MediaId = _testMedia.Id,
            TemplateId = _testTemplate.Id,
            Notes = "Updated notes",
            ConsumedAt = DateTime.UtcNow,
            Scores = [new RankedMediaScoreUpsertRequest
            {
                TemplateFieldId = _testTemplate.Fields.First().Id,
                Value = 10
            }]
        };
        var response = await Client.PostAsJsonAsync(basePath, request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to UPDATE (POST) ranking. Status: {response.StatusCode}. Content: {content}");
        }
        response.EnsureSuccessStatusCode();
        
        var rankedMedia = await response.Content.ReadFromJsonAsync<RankedMediaDto>();
        rankedMedia.Should().NotBeNull();
        rankedMedia!.Id.Should().Be(_testRankedMedia.Id);
        rankedMedia.Notes.Should().Be("Updated notes");
    }
    
    [Fact]
    public async Task DeleteRankedMedia_DeletesRecord()
    {
        var response = await Client.DeleteAsync($"{basePath}/{_testRankedMedia.Id}");
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to DELETE ranking. Status: {response.StatusCode}. Content: {content}");
        }
        response.EnsureSuccessStatusCode();
        
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var exists = await db.RankedMedia.AnyAsync(r => r.Id == _testRankedMedia.Id);
        exists.Should().BeFalse();
    }
}