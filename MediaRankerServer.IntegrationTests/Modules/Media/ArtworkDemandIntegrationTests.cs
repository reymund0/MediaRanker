using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ArtworkDemandIntegrationTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task Browse_RegistersArtworkOnlyForTitlesReturnedOnTheAuthorizedPage()
    {
        long returnedMediaId;
        long excludedMediaId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var returned = new MediaEntity
            {
                Title = "Demand page A returned movie",
                MediaTypeId = -3,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0000001",
                ReleaseDate = new DateOnly(2020, 1, 1)
            };
            var excluded = new MediaEntity
            {
                Title = "Demand page Z excluded movie",
                MediaTypeId = -3,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0000002",
                ReleaseDate = new DateOnly(2020, 1, 2)
            };
            db.Media.AddRange(returned, excluded);
            await db.SaveChangesAsync();
            returnedMediaId = returned.Id;
            excludedMediaId = excluded.Id;
        }

        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<TmdbOptions>(options =>
            {
                options.Enabled = true;
                options.ReadAccessToken = "test-token";
            })));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/media?mediaTypeId=-3&searchField=title&searchTerm=Demand%20page&page=0&pageSize=1&sortField=title&sortDirection=asc&includeTotalCount=true");

        response.IsSuccessStatusCode.Should().BeTrue();
        var page = await response.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle(item => item.Id == returnedMediaId);
        page.Items.Single().CoverStatus.Should().Be("pending");
        page.TotalCount.Should().Be(2);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var covers = await verifyDb.MediaCovers.ToListAsync();
        covers.Should().ContainSingle();
        covers[0].LookupId.Should().Be("tt0000001");
        covers.Should().NotContain(cover => cover.LookupId == "tt0000002");
        excludedMediaId.Should().NotBe(returnedMediaId);
    }

    [Fact]
    public async Task ConcurrentBrowseRequests_CreateOneCanonicalArtworkWorkItem()
    {
        long mediaId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var media = new MediaEntity
            {
                Title = "Concurrent demand movie",
                MediaTypeId = -3,
                ExternalSource = MediaExternalSource.Imdb,
                ExternalId = "tt0000042",
                ReleaseDate = new DateOnly(2020, 1, 1)
            };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            mediaId = media.Id;
        }

        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<TmdbOptions>(options =>
            {
                options.Enabled = true;
                options.ReadAccessToken = "test-token";
            })));
        using var client = factory.CreateClient();

        var requests = await Task.WhenAll(
            client.GetAsync("/api/media?mediaTypeId=-3&searchField=title&searchTerm=Concurrent%20demand&page=0&pageSize=10"),
            client.GetAsync("/api/media?mediaTypeId=-3&searchField=title&searchTerm=Concurrent%20demand&page=0&pageSize=10"));

        requests.Should().OnlyContain(response => response.IsSuccessStatusCode);
        var pages = await Task.WhenAll(requests.Select(response => response.Content.ReadFromJsonAsync<PageResult<MediaDto>>()));
        pages.Should().OnlyContain(page => page!.Items.Single(item => item.Id == mediaId).CoverStatus == "pending");

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var covers = await verifyDb.MediaCovers.ToListAsync();
        covers.Should().ContainSingle(cover =>
            cover.Provider == ArtworkProvider.Tmdb
            && cover.LookupKind == CoverLookupKind.MovieImdb
            && cover.LookupId == "tt0000042");
    }
}
