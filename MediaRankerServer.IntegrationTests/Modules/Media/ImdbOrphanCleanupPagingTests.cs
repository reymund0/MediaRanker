using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public sealed class ImdbOrphanCleanupPagingTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task ValidFirstPageAdvancesToLaterOrphansAndFinishesWithTerminalPage()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.ImdbImports.AddRange(Import("tt9400001"), Import("tt9400002"), Import("tt9400004"));
        db.ImdbImportEpisodes.AddRange(
            Episode("tt9400001"),
            Episode("tt9400002"),
            Episode("tt9400003"),
            Episode("tt9400004"));
        await db.SaveChangesAsync();

        var episodeIds = await db.ImdbImportEpisodes.AsNoTracking()
            .OrderBy(episode => episode.Id)
            .Select(episode => new { episode.Id, episode.Tconst })
            .ToListAsync();
        var provider = CreateProvider(db);

        var first = await provider.DeleteOrphanEpisodesBatchAsync(null, 2, CancellationToken.None);
        var second = await provider.DeleteOrphanEpisodesBatchAsync(first.NextId, 2, CancellationToken.None);
        var terminal = await provider.DeleteOrphanEpisodesBatchAsync(second.NextId, 2, CancellationToken.None);

        first.Affected.Should().Be(0);
        first.NextId.Should().Be(episodeIds[1].Id);
        first.HasMore.Should().BeTrue();
        second.Affected.Should().Be(1);
        second.NextId.Should().Be(episodeIds[3].Id);
        second.HasMore.Should().BeTrue();
        terminal.Affected.Should().Be(0);
        terminal.NextId.Should().BeNull();
        terminal.HasMore.Should().BeFalse();
        (await db.ImdbImportEpisodes.AsNoTracking().Select(episode => episode.Tconst).ToListAsync())
            .Should().BeEquivalentTo("tt9400001", "tt9400002", "tt9400004");
    }

    [Fact]
    public async Task FullAndPartialPagesDeleteAtMostTheCandidateLimitAndReplayIsSafe()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.ImdbImportEpisodes.AddRange(Episode("tt9500001"), Episode("tt9500002"), Episode("tt9500003"));
        await db.SaveChangesAsync();

        var provider = CreateProvider(db);
        var full = await provider.DeleteOrphanEpisodesBatchAsync(null, 2, CancellationToken.None);
        var partial = await provider.DeleteOrphanEpisodesBatchAsync(full.NextId, 2, CancellationToken.None);
        var replay = await provider.DeleteOrphanEpisodesBatchAsync(null, 2, CancellationToken.None);

        full.Affected.Should().Be(2);
        full.Affected.Should().BeLessThanOrEqualTo(2);
        full.NextId.Should().NotBeNull();
        full.HasMore.Should().BeTrue();
        partial.Affected.Should().Be(1);
        partial.Affected.Should().BeLessThanOrEqualTo(2);
        partial.NextId.Should().NotBeNull();
        partial.HasMore.Should().BeFalse();
        replay.Affected.Should().Be(0);
        replay.NextId.Should().BeNull();
        replay.HasMore.Should().BeFalse();
        (await db.ImdbImportEpisodes.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CancellationLeavesOrphanEpisodeAvailableForReplay()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.ImdbImportEpisodes.Add(Episode("tt9600001"));
        await db.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var operation = () => CreateProvider(db).DeleteOrphanEpisodesBatchAsync(null, 10, cancellation.Token);

        await operation.Should().ThrowAsync<OperationCanceledException>();
        (await db.ImdbImportEpisodes.AsNoTracking().CountAsync()).Should().Be(1);
    }

    private static ImdbImportSqlProvider CreateProvider(PostgreSQLContext db) =>
        new(db, NullLogger<ImdbImportSqlProvider>.Instance, Options.Create(new ImdbImportOptions()));

    private static ImdbImport Import(string tconst) => new()
    {
        Tconst = tconst,
        TitleType = "tvEpisode",
        PrimaryTitle = tconst,
        OriginalTitle = tconst,
        IsAdult = false,
        RawLine = tconst
    };

    private static ImdbImportEpisode Episode(string tconst) => new()
    {
        Tconst = tconst,
        ParentTconst = "tt9000000",
        SeasonNumber = 1,
        EpisodeNumber = 1,
        RawLine = tconst
    };
}
