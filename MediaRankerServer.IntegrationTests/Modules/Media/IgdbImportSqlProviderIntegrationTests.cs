using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class IgdbImportSqlProviderIntegrationTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task CommitPageAsync_FailedPageDoesNotAdvanceCursor_AndReplayCommitsOnce()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var now = DateTimeOffset.UtcNow;
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        await provider.StartRunAsync(lease, bootstrap: true, maximumId: 100, null, null, now, CancellationToken.None);

        var duplicatePage = new[] { Staged(10, now), Staged(10, now) };
        var failed = () => provider.CommitPageAsync(lease, duplicatePage, 10, CancellationToken.None);
        await failed.Should().ThrowAsync<DbUpdateException>();

        var stateAfterFailure = await db.Set<IgdbImportState>().AsNoTracking().SingleAsync();
        stateAfterFailure.LastCommittedId.Should().Be(0);
        (await db.Set<IgdbImport>().AsNoTracking().CountAsync()).Should().Be(0);

        using var replayScope = Factory.Services.CreateScope();
        var replayDb = replayScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var replayProvider = new IgdbImportSqlProvider(replayDb, NullLogger<IgdbImportSqlProvider>.Instance);
        await replayProvider.CommitPageAsync(lease, [Staged(10, now)], 10, CancellationToken.None);
        await replayProvider.ReleaseLeaseAsync(lease, CancellationToken.None);

        (await replayDb.Set<IgdbImport>().AsNoTracking().CountAsync()).Should().Be(1);
        (await replayDb.Set<IgdbImportState>().AsNoTracking().SingleAsync()).LastCommittedId.Should().Be(10);
    }

    [Fact]
    public async Task CommitPageAsync_EqualProviderVersionRefreshesMetadataOnlyForNewerFetch()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var providerVersion = DateTimeOffset.UtcNow.AddHours(-2);
        var firstFetch = providerVersion.AddMinutes(1);
        await provider.StartRunAsync(lease, bootstrap: true, maximumId: 100, null, null, DateTimeOffset.UtcNow, CancellationToken.None);
        await provider.CommitPageAsync(lease, [Staged(42, firstFetch, cover: "old-cover", name: "Old title", providerUpdatedAt: providerVersion)], 42, CancellationToken.None);

        var newerFetch = firstFetch.AddMinutes(1);
        await provider.CommitPageAsync(lease, [Staged(42, newerFetch, cover: "new-cover", name: "New title", providerUpdatedAt: providerVersion)], 42, CancellationToken.None);
        var refreshed = await db.Set<IgdbImport>().AsNoTracking().SingleAsync(x => x.IgdbGameId == 42);
        refreshed.Name.Should().Be("New title");
        refreshed.CoverImageId.Should().Be("new-cover");
        refreshed.FetchedAt.Should().BeCloseTo(newerFetch, TimeSpan.FromMilliseconds(1));

        await provider.CommitPageAsync(lease, [Staged(42, firstFetch, cover: "stale-cover", name: "Stale title", providerUpdatedAt: providerVersion)], 42, CancellationToken.None);
        var unchanged = await db.Set<IgdbImport>().AsNoTracking().SingleAsync(x => x.IgdbGameId == 42);
        unchanged.Name.Should().Be("New title");
        unchanged.CoverImageId.Should().Be("new-cover");
        unchanged.FetchedAt.Should().BeCloseTo(newerFetch, TimeSpan.FromMilliseconds(1));

        await provider.CommitPageAsync(lease, [Staged(42, newerFetch.AddMinutes(1), cover: "old-provider-cover", name: "Old provider title", providerUpdatedAt: providerVersion.AddMinutes(-1))], 42, CancellationToken.None);
        var providerGuarded = await db.Set<IgdbImport>().AsNoTracking().SingleAsync(x => x.IgdbGameId == 42);
        providerGuarded.Name.Should().Be("New title");
        providerGuarded.CoverImageId.Should().Be("new-cover");
        providerGuarded.ProviderUpdatedAt.Should().NotBeNull();
        providerGuarded.ProviderUpdatedAt!.Value.Should().BeCloseTo(providerVersion, TimeSpan.FromMilliseconds(1));

        await provider.CommitPageAsync(lease, [Staged(42, firstFetch, cover: "new-provider-stale-fetch", name: "New provider stale fetch", providerUpdatedAt: providerVersion.AddMinutes(1))], 42, CancellationToken.None);
        var fetchGuarded = await db.Set<IgdbImport>().AsNoTracking().SingleAsync(x => x.IgdbGameId == 42);
        fetchGuarded.Name.Should().Be("New title");
        fetchGuarded.CoverImageId.Should().Be("new-cover");
        fetchGuarded.ProviderUpdatedAt.Should().NotBeNull();
        fetchGuarded.ProviderUpdatedAt!.Value.Should().BeCloseTo(providerVersion, TimeSpan.FromMilliseconds(1));
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task LoadEligibleGamesAsync_ChangedMetadataAndCoverRefreshesSameDomainIdentity()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var now = DateTimeOffset.UtcNow;
        var providerVersion = now.AddHours(-3);
        var firstFetch = now.AddHours(-2);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        await provider.StartRunAsync(lease, bootstrap: true, maximumId: 500, null, null, now, CancellationToken.None);
        await provider.CommitPageAsync(lease, [Staged(301, firstFetch, cover: "cover-before", name: "Title before", providerUpdatedAt: providerVersion)], 301, CancellationToken.None);
        await provider.LoadEligibleGamesAsync(lease, SupportedTypes(), now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);
        var firstMedia = await db.Media.SingleAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "301");
        var firstMediaId = firstMedia.Id;

        // Model a later fetch using the persisted clock so host/container clock skew cannot reorder this fixture.
        var secondFetch = firstMedia.UpdatedAt.AddMinutes(1);
        await provider.CommitPageAsync(lease, [Staged(301, secondFetch, cover: "cover-after", name: "Title after", providerUpdatedAt: providerVersion)], 301, CancellationToken.None);
        (await provider.LoadEligibleGamesAsync(lease, SupportedTypes(), now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None)).Should().Be(1);

        var refreshedMedia = await db.Media.AsNoTracking().SingleAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "301");
        refreshedMedia.Id.Should().Be(firstMediaId);
        refreshedMedia.Title.Should().Be("Title after");
        var refreshedCover = await db.MediaCovers.AsNoTracking().SingleAsync(x => x.LookupId == "301");
        refreshedCover.ImagePath.Should().Be("cover-after");
        refreshedCover.CheckedAt.Should().NotBeNull();
        refreshedCover.CheckedAt!.Value.Should().BeCloseTo(secondFetch, TimeSpan.FromMilliseconds(1));
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task LoadEligibleGamesAsync_UnchangedNewerFetchRefreshesOnceThenLocalReplayIsSkipped()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var now = DateTimeOffset.UtcNow;
        var providerVersion = now.AddHours(-3);
        var firstFetch = now.AddHours(-2);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        await provider.StartRunAsync(lease, bootstrap: true, maximumId: 500, null, null, now, CancellationToken.None);
        var releaseDate = now.AddDays(-1);
        await provider.CommitPageAsync(lease, [Staged(302, firstFetch, cover: "same-cover", name: "Same title", release: releaseDate, providerUpdatedAt: providerVersion)], 302, CancellationToken.None);
        (await provider.LoadEligibleGamesAsync(lease, SupportedTypes(), now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None)).Should().Be(1);

        var firstCover = await db.MediaCovers.AsNoTracking().SingleAsync(x => x.LookupId == "302");
        var firstExpiry = firstCover.ExpiresAt;
        var persistedMedia = await db.Media.AsNoTracking().SingleAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "302");
        var newerFetch = new[] { firstFetch, firstCover.CheckedAt!.Value, persistedMedia.UpdatedAt }.Max().AddMinutes(1);
        await provider.CommitPageAsync(lease, [Staged(302, newerFetch, cover: "same-cover", name: "Same title", release: releaseDate, providerUpdatedAt: providerVersion)], 302, CancellationToken.None);

        (await provider.LoadEligibleGamesAsync(lease, SupportedTypes(), now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None)).Should().Be(1);
        var refreshedCover = await db.MediaCovers.AsNoTracking().SingleAsync(x => x.LookupId == "302");
        refreshedCover.CheckedAt.Should().BeCloseTo(newerFetch, TimeSpan.FromMilliseconds(1));
        refreshedCover.ExpiresAt.Should().NotBe(firstExpiry);

        // Replaying the same staged fetch must be a no-op and must not renew its cache lifetime.
        (await provider.LoadEligibleGamesAsync(lease, SupportedTypes(), now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None)).Should().Be(0);
        var unchangedCover = await db.MediaCovers.AsNoTracking().SingleAsync(x => x.LookupId == "302");
        unchangedCover.CheckedAt.Should().Be(refreshedCover.CheckedAt);
        unchangedCover.ExpiresAt.Should().Be(refreshedCover.ExpiresAt);
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task LoadEligibleGamesAsync_AdmitsReleasedSupportedGamesAndPreservesNewerArtwork()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        db.Set<IgdbImport>().AddRange(
            Staged(1, now.AddDays(-2), "Main game", release: now.AddDays(-1), cover: "imported"),
            Staged(2, now, "DLC", release: now.AddDays(-1)),
            Staged(3, now, "Main game", release: now.AddDays(1)),
            Staged(4, now, "Remake", release: now.AddDays(-1), versionParentId: 1));
        await db.SaveChangesAsync();

        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var supported = SupportedTypes();
        var loaded = await provider.LoadEligibleGamesAsync(lease, supported, now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);

        loaded.Should().Be(1);
        var cover = await db.Set<MediaCover>().SingleAsync(x => x.LookupId == "1");
        cover.Outcome.Should().Be(CoverOutcome.Ready);
        cover.ImagePath.Should().Be("imported");

        cover.ImagePath = "worker-new";
        cover.CheckedAt = now;
        cover.ExpiresAt = now.AddDays(30);
        cover.Version++;
        await db.SaveChangesAsync();
        var refreshed = await provider.LoadEligibleGamesAsync(lease, supported, now.AddMinutes(1), TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);

        refreshed.Should().Be(0, "unchanged staged rows need no local reload");
        (await db.Set<MediaCover>().SingleAsync(x => x.LookupId == "1")).ImagePath.Should().Be("worker-new");

        var future = await db.Set<IgdbImport>().SingleAsync(x => x.IgdbGameId == 3);
        future.FirstReleaseDate = now.AddDays(-1);
        await db.SaveChangesAsync();
        var releasedLater = await provider.LoadEligibleGamesAsync(lease, supported, now.AddMinutes(1), TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);
        releasedLater.Should().Be(1, "only the newly released game needs admission");

        (await db.Media.Where(x => x.ExternalSource == MediaExternalSource.Igdb).Select(x => x.ExternalId).ToListAsync())
            .Should().BeEquivalentTo(["1", "3"]);
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task LoadEligibleGamesAsync_AdmitsSupportedTypesIncludingLaterToday_AndExcludesUnsupportedOrUnknownRows()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        var laterToday = new DateTimeOffset(now.UtcDateTime.Date.AddHours(23), TimeSpan.Zero);
        db.Set<IgdbImport>().AddRange(
            Staged(101, now.AddHours(-3), "Main game", release: laterToday, cover: "main-cover"),
            Staged(102, now.AddHours(-3), "Remake", release: now.AddDays(-1)),
            Staged(103, now.AddHours(-3), "Remaster", release: now.AddDays(-2), cover: "remaster-cover"),
            Staged(104, now.AddHours(-3), "DLC", release: now.AddDays(-1)),
            Staged(105, now.AddHours(-3), "Expansion", release: now.AddDays(-1)),
            Staged(106, now.AddHours(-3), "Bundle", release: now.AddDays(-1)),
            Staged(107, now.AddHours(-3), "Port", release: now.AddDays(-1)),
            Staged(108, now.AddHours(-3), "Edition", release: now.AddDays(-1)),
            Staged(109, now.AddHours(-3), "Unknown current type", release: now.AddDays(-1)),
            Staged(110, now.AddHours(-3), "Main game", undated: true),
            Staged(111, now.AddHours(-3), "Main game", release: now.AddDays(1)),
            Staged(112, now.AddHours(-3), "Remake", release: now.AddDays(-1), versionParentId: 101));
        await db.SaveChangesAsync();

        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var supported = SupportedTypes();
        var loaded = await provider.LoadEligibleGamesAsync(lease, supported, now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);

        loaded.Should().Be(3);
        (await db.Media.AsNoTracking().Where(x => x.ExternalSource == MediaExternalSource.Igdb).Select(x => x.ExternalId).ToListAsync())
            .Should().BeEquivalentTo(["101", "102", "103"]);
        (await db.MediaCovers.AsNoTracking().SingleAsync(x => x.LookupId == "102")).Outcome.Should().Be(CoverOutcome.Missing);
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task LoadEligibleGamesAsync_ReleaseDayAdmissionRunsWithoutUpstreamChanges()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        var laterToday = new DateTimeOffset(now.UtcDateTime.Date.AddHours(23), TimeSpan.Zero);
        var staged = Staged(201, now.AddDays(-3), "Main game", release: laterToday, cover: "release-day-cover");
        db.Set<IgdbImport>().Add(staged);
        await db.SaveChangesAsync();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var loaded = await provider.LoadEligibleGamesAsync(lease,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Main game" }, now,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);

        loaded.Should().Be(1);
        (await db.Media.AsNoTracking().SingleAsync(x => x.ExternalId == "201")).Title.Should().Be("Game 201");
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task LoadEligibleGamesAsync_UsesMultipleBoundedBatches_AndDoesNotRenewUnchangedReferences()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        db.Set<IgdbImport>().AddRange(Enumerable.Range(1, 503).Select(id => Staged(id, now.AddDays(-1), "Main Game", cover: "co123")));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var supported = SupportedTypes();
        var loaded = await provider.LoadEligibleGamesAsync(lease, supported, now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);
        loaded.Should().Be(503);
        lease.State.Version.Should().BeGreaterThanOrEqualTo(3, "each bounded batch commits and renews its lease");
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Media.CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb)).Should().Be(503);
        var firstExpiry = await db.MediaCovers.AsNoTracking().Select(x => x.ExpiresAt).FirstAsync();
        (await provider.LoadEligibleGamesAsync(lease, supported, now, TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None)).Should().Be(0);
        (await db.MediaCovers.AsNoTracking().Select(x => x.ExpiresAt).FirstAsync()).Should().Be(firstExpiry);
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    private static IgdbImport Staged(long id, DateTimeOffset fetchedAt, string gameType = "Main game", DateTimeOffset? release = null, string? cover = null, long? versionParentId = null, bool undated = false, string? name = null, DateTimeOffset? providerUpdatedAt = null) => new()
    {
        IgdbGameId = id,
        Name = name ?? $"Game {id}",
        GameTypeName = gameType,
        FirstReleaseDate = undated ? null : release ?? fetchedAt.AddDays(-1),
        CoverImageId = cover,
        VersionParentId = versionParentId,
        ProviderUpdatedAt = providerUpdatedAt ?? fetchedAt,
        FetchedAt = fetchedAt
    };

    private static HashSet<string> SupportedTypes() => new(StringComparer.OrdinalIgnoreCase) { "Main game", "Remake", "Remaster" };
}
