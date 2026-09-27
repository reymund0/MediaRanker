using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class MediaCoverPersistenceTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task CanonicalCoverKey_DuplicateProviderLookup_IsRejected()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        db.MediaCovers.Add(CreatePendingCover());
        await db.SaveChangesAsync();

        db.MediaCovers.Add(CreatePendingCover());
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ReadyCover_WithoutRequiredProviderMetadata_IsRejected()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var cover = CreatePendingCover();
        cover.Outcome = CoverOutcome.Ready;
        db.MediaCovers.Add(cover);

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PendingCover_WithCanonicalIdentity_PersistsWithoutImageMetadata()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var cover = CreatePendingCover();
        db.MediaCovers.Add(cover);
        await db.SaveChangesAsync();

        var stored = await db.MediaCovers.SingleAsync();
        stored.Outcome.Should().Be(CoverOutcome.Pending);
        stored.ImagePath.Should().BeNull();
        stored.ExpiresAt.Should().BeNull();
    }

    private static MediaCover CreatePendingCover() => new()
    {
        Provider = ArtworkProvider.Tmdb,
        LookupKind = CoverLookupKind.MovieImdb,
        LookupId = "tt0133093",
        Outcome = CoverOutcome.Pending
    };
}
