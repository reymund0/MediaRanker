using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

// These should be unit tests, but due to the usage of ExecuteUpdateAsync
// which is not supported by the InMemory database, they are integration tests for now.
public class MediaCoverCleanupTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const long MovieMediaTypeId = -3;

    [Fact]
    public async Task CleanupAsync_WhenCoverIsMarkedAndUnreferenced_RemovesCover()
    {
        var coverId = await SeedCoverAsync("marked-cover", markedForCleanup: true);

        await RunCleanupAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var cover = await db.MediaCovers.FirstOrDefaultAsync(c => c.Id == coverId);

        cover.Should().BeNull();
    }

    [Fact]
    public async Task CleanupAsync_WhenCoverIsUnreferencedAndNotMarked_MarksCoverForCleanup()
    {
        var coverId = await SeedCoverAsync("unmarked-cover", markedForCleanup: false);

        await RunCleanupAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var cover = await db.MediaCovers.FirstOrDefaultAsync(c => c.Id == coverId);

        cover.Should().NotBeNull();
        cover!.MarkedForCleanup.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupAsync_WhenCoverIsMarkedButReferenced_UnmarksCover()
    {
        var coverId = await SeedCoverAsync("referenced-cover", markedForCleanup: true);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            db.Media.Add(new MediaEntity
            {
                Title = "Referenced Media",
                MediaTypeId = MovieMediaTypeId,
                CoverId = coverId,
                ReleaseDate = new DateOnly(2024, 1, 1)
            });
            await db.SaveChangesAsync();
        }

        await RunCleanupAsync();

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var cover = await verifyDb.MediaCovers.FirstOrDefaultAsync(c => c.Id == coverId);

        cover.Should().NotBeNull();
        cover!.MarkedForCleanup.Should().BeFalse();
    }

    private async Task RunCleanupAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IMediaCoverCleanupService>();
        await cleanupService.CleanupAsync(CancellationToken.None);
    }

    private async Task<long> SeedCoverAsync(string fileKey, bool markedForCleanup)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var cover = new MediaCover
        {
            FileUploadId = Random.Shared.NextInt64(1, long.MaxValue),
            FileKey = fileKey,
            FileName = $"{fileKey}.png",
            FileSizeBytes = 1024,
            FileContentType = "image/png",
            MarkedForCleanup = markedForCleanup,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.MediaCovers.Add(cover);
        await db.SaveChangesAsync();

        return cover.Id;
    }
}
