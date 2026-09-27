using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

public class ArtworkProcessorIntegrationTests(PostgresContainerFixture postgresFixture, LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    [Fact]
    public async Task RunAsync_WhenTmdbFails_RetainsDelayedFailureWithoutCallingLiveProvider()
    {
        long coverId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var now = DateTimeOffset.UtcNow;
            var cover = new MediaCover
            {
                Provider = ArtworkProvider.Tmdb,
                LookupKind = CoverLookupKind.MovieImdb,
                LookupId = "tt0133093",
                Outcome = CoverOutcome.Pending,
                RequestedAt = now,
                NextAttemptAt = now,
                AttemptCount = 0
            };
            db.MediaCovers.Add(cover);
            await db.SaveChangesAsync();
            coverId = cover.Id;
        }

        using var processorScope = Factory.Services.CreateScope();
        var processorDb = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var igdb = new UnusedIgdbClient();
        var tmdb = new FailingTmdbClient();
        var processor = new ArtworkProcessor(
            processorDb,
            igdb,
            tmdb,
            Options.Create(new IgdbOptions()),
            Options.Create(new TmdbOptions { Enabled = true, ReadAccessToken = "test-token" }),
            Options.Create(new ArtworkOptions { BatchSize = 1, RetrySeconds = 60, MaxRetrySeconds = 60 }),
            TimeProvider.System,
            NullLogger<ArtworkProcessor>.Instance,
            new IgdbRequestLimiter(new IgdbOptions()), new TmdbRequestCooldown());

        var before = DateTimeOffset.UtcNow;
        await processor.RunAsync(CancellationToken.None);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Failed);
        stored.FailureCode.Should().Be("provider_unavailable");
        stored.ClaimToken.Should().BeNull();
        stored.ClaimedUntil.Should().BeNull();
        stored.NextAttemptAt.Should().NotBeNull();
        stored.NextAttemptAt!.Value.Should().BeOnOrAfter(before.AddSeconds(59));
        tmdb.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_ExpiredTmdbReference_IsPurgedWhenAllProvidersAreDisabled()
    {
        long coverId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var cover = new MediaCover
            {
                Provider = ArtworkProvider.Tmdb,
                LookupKind = CoverLookupKind.MovieImdb,
                LookupId = "tt0133093",
                Outcome = CoverOutcome.Ready,
                ProviderItemId = "603",
                ImagePath = "/old.jpg",
                CheckedAt = DateTimeOffset.UtcNow.AddDays(-31),
                ExpiresAt = DateTimeOffset.UtcNow.AddTicks(-1)
            };
            db.MediaCovers.Add(cover);
            await db.SaveChangesAsync();
            coverId = cover.Id;
        }

        using var processorScope = Factory.Services.CreateScope();
        var processorDb = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        await CreateProcessor(processorDb, new StaticTmdbClient(new("603", "/unused.jpg")), tmdbEnabled: false)
            .RunAsync(CancellationToken.None);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Missing);
        stored.ImagePath.Should().BeNull();
        stored.ProviderItemId.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ExpiredClaim_IsRecoveredAndCompleted()
    {
        long coverId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var now = DateTimeOffset.UtcNow;
            var cover = CreatePendingTmdbCover(now);
            cover.ClaimToken = Guid.NewGuid();
            cover.ClaimedUntil = now.AddMinutes(-1);
            cover.AttemptCount = 1;
            db.MediaCovers.Add(cover);
            await db.SaveChangesAsync();
            coverId = cover.Id;
        }

        using var processorScope = Factory.Services.CreateScope();
        var processorDb = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        await CreateProcessor(processorDb, new StaticTmdbClient(new("603", "/matrix.jpg")), tmdbEnabled: true)
            .RunAsync(CancellationToken.None);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Ready);
        stored.ImagePath.Should().Be("/matrix.jpg");
        stored.ClaimToken.Should().BeNull();
        stored.ClaimedUntil.Should().BeNull();
        stored.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WhenProviderReportsNoPoster_CachesMissingResult()
    {
        var coverId = await SeedPendingTmdbCoverAsync();

        using var processorScope = Factory.Services.CreateScope();
        var db = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var before = DateTimeOffset.UtcNow;
        await CreateProcessor(db, new StaticTmdbClient(new(null, null)), tmdbEnabled: true)
            .RunAsync(CancellationToken.None);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Missing);
        stored.ImagePath.Should().BeNull();
        stored.CheckedAt.Should().NotBeNull();
        stored.ExpiresAt.Should().NotBeNull();
        stored.ExpiresAt!.Value.Should().BeOnOrAfter(before.AddDays(6).AddHours(23));
        stored.NextAttemptAt.Should().Be(stored.ExpiresAt);
    }

    [Fact]
    public async Task RunAsync_WhenProviderRemovesReadyPoster_ClearsStaleProviderData()
    {
        long coverId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var now = DateTimeOffset.UtcNow;
            var cover = new MediaCover
            {
                Provider = ArtworkProvider.Tmdb,
                LookupKind = CoverLookupKind.MovieImdb,
                LookupId = "tt0133093",
                Outcome = CoverOutcome.Ready,
                ProviderItemId = "603",
                ImagePath = "/old.jpg",
                CheckedAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(30),
                RequestedAt = now,
                NextAttemptAt = now
            };
            db.MediaCovers.Add(cover);
            await db.SaveChangesAsync();
            coverId = cover.Id;
        }

        using var processorScope = Factory.Services.CreateScope();
        var processorDb = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var tmdb = new StaticTmdbClient(new(null, null));
        await CreateProcessor(processorDb, tmdb, tmdbEnabled: true)
            .RunAsync(CancellationToken.None);
        tmdb.CallCount.Should().Be(1);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Missing);
        stored.ProviderItemId.Should().BeNull();
        stored.ImagePath.Should().BeNull();
        stored.ExpiresAt.Should().NotBeNull();
        stored.NextAttemptAt.Should().Be(stored.ExpiresAt);
        var presentation = ArtworkPresentation.Map(stored, DateTimeOffset.UtcNow);
        presentation.Url.Should().BeNull();
        presentation.Status.Should().Be("missing");
    }

    [Fact]
    public async Task RunAsync_WhenProvidersAreDisabled_DoesNotCallClientsOrClaimWork()
    {
        var coverId = await SeedPendingTmdbCoverAsync();
        var tmdb = new StaticTmdbClient(new("603", "/matrix.jpg"));

        using var processorScope = Factory.Services.CreateScope();
        var db = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        await CreateProcessor(db, tmdb, tmdbEnabled: false).RunAsync(CancellationToken.None);

        tmdb.CallCount.Should().Be(0);
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Pending);
        stored.ClaimToken.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_WhenClaimHasExhaustedAttempts_ReleasesItAsDelayedFailureWithoutCallingProvider()
    {
        long coverId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var cover = CreatePendingTmdbCover(DateTimeOffset.UtcNow);
            cover.AttemptCount = 5;
            cover.ClaimToken = Guid.NewGuid();
            cover.ClaimedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
            db.MediaCovers.Add(cover);
            await db.SaveChangesAsync();
            coverId = cover.Id;
        }

        var tmdb = new StaticTmdbClient(new("603", "/matrix.jpg"));
        using var processorScope = Factory.Services.CreateScope();
        var processorDb = processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var before = DateTimeOffset.UtcNow;
        await CreateProcessor(processorDb, tmdb, tmdbEnabled: true).RunAsync(CancellationToken.None);

        tmdb.CallCount.Should().Be(0);
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.Outcome.Should().Be(CoverOutcome.Failed);
        stored.ClaimToken.Should().BeNull();
        stored.ClaimedUntil.Should().BeNull();
        stored.NextAttemptAt.Should().NotBeNull();
        stored.NextAttemptAt!.Value.Should().BeOnOrAfter(before.AddSeconds(59));
    }

    [Fact]
    public async Task RunAsync_ConcurrentProcessors_CreateOnlyOneActiveClaim()
    {
        await SeedPendingTmdbCoverAsync();
        var tmdb = new BlockingTmdbClient(new("603", "/matrix.jpg"));

        using var firstScope = Factory.Services.CreateScope();
        using var secondScope = Factory.Services.CreateScope();
        var first = CreateProcessor(firstScope.ServiceProvider.GetRequiredService<PostgreSQLContext>(), tmdb, tmdbEnabled: true)
            .RunAsync(CancellationToken.None);
        await tmdb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = CreateProcessor(secondScope.ServiceProvider.GetRequiredService<PostgreSQLContext>(), tmdb, tmdbEnabled: true)
            .RunAsync(CancellationToken.None);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        tmdb.Complete();
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        tmdb.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_DelayedCompletionCannotOverwriteNewerCoverResult()
    {
        var coverId = await SeedPendingTmdbCoverAsync();
        var tmdb = new BlockingTmdbClient(new("603", "/stale.jpg"));

        using var processorScope = Factory.Services.CreateScope();
        var processor = CreateProcessor(processorScope.ServiceProvider.GetRequiredService<PostgreSQLContext>(), tmdb, tmdbEnabled: true);
        var run = processor.RunAsync(CancellationToken.None);
        await tmdb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using (var updateScope = Factory.Services.CreateScope())
        {
            var updateDb = updateScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var newer = await updateDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
            newer.Outcome = CoverOutcome.Ready;
            newer.ProviderItemId = "newer-item";
            newer.ImagePath = "/newer.jpg";
            newer.CheckedAt = DateTimeOffset.UtcNow;
            newer.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
            newer.Version++;
            await updateDb.SaveChangesAsync();
        }

        tmdb.Complete();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var stored = await verifyDb.MediaCovers.SingleAsync(cover => cover.Id == coverId);
        stored.ImagePath.Should().Be("/newer.jpg");
        stored.ProviderItemId.Should().Be("newer-item");
        stored.Outcome.Should().Be(CoverOutcome.Ready);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    public async Task RunAsync_CoolingProviderPreservesAttemptsAndHealthyProviderProgresses(bool blockIgdb, bool cooldownAfterClaim, bool actualProviderFailure)
    {
        var now = DateTimeOffset.UtcNow;
        var igdbSettings = new IgdbOptions { ArtworkEnabled = true, ClientId = "test", ClientSecret = "test" };
        var igdbLimiter = new IgdbRequestLimiter(igdbSettings);
        var tmdbCooldown = new TmdbRequestCooldown();
        async Task BlockProvider()
        {
            if (blockIgdb) await igdbLimiter.DeferAsync(TimeSpan.FromMinutes(5), "authentication_failed", CancellationToken.None);
            else tmdbCooldown.Defer(TimeSpan.FromMinutes(5));
        }

        if (!cooldownAfterClaim) await BlockProvider();
        var igdbCalls = 0;
        var tmdbCalls = 0;
        async Task<ArtworkResult> Lookup(bool isIgdb)
        {
            if (isIgdb == blockIgdb && cooldownAfterClaim)
            {
                await BlockProvider();
                if (actualProviderFailure)
                {
                    if (isIgdb) igdbCalls++;
                    else tmdbCalls++;
                    throw new ProviderRequestException("rate_limited", TimeSpan.FromMinutes(5));
                }
            }
            if (isIgdb) igdbLimiter.ThrowIfBlocked();
            else tmdbCooldown.ThrowIfBlocked();
            if (isIgdb) igdbCalls++;
            else tmdbCalls++;
            return isIgdb ? new("1", "co123") : new("603", "/matrix.jpg");
        }

        long blockedId;
        long healthyId;
        using (var seedScope = Factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
            var igdbCover = new MediaCover
            {
                Provider = ArtworkProvider.Igdb, LookupKind = CoverLookupKind.IgdbGame, LookupId = "1",
                Outcome = CoverOutcome.Pending, RequestedAt = now, NextAttemptAt = now.AddMinutes(-1)
            };
            var tmdbCover = CreatePendingTmdbCover(now.AddMinutes(-1));
            var blocked = blockIgdb ? igdbCover : tmdbCover;
            var healthy = blockIgdb ? tmdbCover : igdbCover;
            blocked.NextAttemptAt = now.AddMinutes(-2);
            blocked.AttemptCount = 4;
            db.MediaCovers.AddRange(blocked, healthy);
            await db.SaveChangesAsync();
            blockedId = blocked.Id;
            healthyId = healthy.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var processor = new ArtworkProcessor(scope.ServiceProvider.GetRequiredService<PostgreSQLContext>(),
                new CallbackIgdbClient(() => Lookup(true)), new CallbackTmdbClient(() => Lookup(false)),
                Options.Create(igdbSettings), Options.Create(new TmdbOptions { Enabled = true, ReadAccessToken = "test" }),
                Options.Create(new ArtworkOptions { BatchSize = 2 }), TimeProvider.System,
                NullLogger<ArtworkProcessor>.Instance, igdbLimiter, tmdbCooldown);
            await processor.RunAsync(CancellationToken.None);
        }

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var blockedStored = await verifyDb.MediaCovers.SingleAsync(x => x.Id == blockedId);
        blockedStored.AttemptCount.Should().Be(actualProviderFailure ? 5 : 4);
        blockedStored.ClaimToken.Should().BeNull();
        if (cooldownAfterClaim) blockedStored.NextAttemptAt.Should().BeOnOrAfter(now.AddMinutes(5));
        (await verifyDb.MediaCovers.SingleAsync(x => x.Id == healthyId)).Outcome.Should().Be(CoverOutcome.Ready);
        igdbCalls.Should().Be(blockIgdb && !actualProviderFailure ? 0 : 1);
        tmdbCalls.Should().Be(!blockIgdb && !actualProviderFailure ? 0 : 1);
    }

    private sealed class CallbackTmdbClient(Func<Task<ArtworkResult>> lookup) : ITmdbClient
    {
        public Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct) => lookup();
    }

    private sealed class CallbackIgdbClient(Func<Task<ArtworkResult>> lookup) : IIgdbClient
    {
        public Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct) => lookup();
        public Task<long> GetMaximumGameIdAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct) => throw new NotSupportedException();
    }

    private async Task<long> SeedPendingTmdbCoverAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var cover = CreatePendingTmdbCover(DateTimeOffset.UtcNow);
        db.MediaCovers.Add(cover);
        await db.SaveChangesAsync();
        return cover.Id;
    }

    private static MediaCover CreatePendingTmdbCover(DateTimeOffset now) => new()
    {
        Provider = ArtworkProvider.Tmdb,
        LookupKind = CoverLookupKind.MovieImdb,
        LookupId = "tt0133093",
        Outcome = CoverOutcome.Pending,
        RequestedAt = now,
        NextAttemptAt = now
    };

    private static ArtworkProcessor CreateProcessor(PostgreSQLContext db, ITmdbClient tmdb, bool tmdbEnabled) => new(
        db,
        new UnusedIgdbClient(),
        tmdb,
        Options.Create(new IgdbOptions()),
        Options.Create(new TmdbOptions { Enabled = tmdbEnabled, ReadAccessToken = tmdbEnabled ? "test-token" : null }),
        Options.Create(new ArtworkOptions { BatchSize = 1, RetrySeconds = 60, MaxRetrySeconds = 60 }),
        TimeProvider.System,
        NullLogger<ArtworkProcessor>.Instance,
        new IgdbRequestLimiter(new IgdbOptions()), new TmdbRequestCooldown());

    private sealed class FailingTmdbClient : ITmdbClient
    {
        public int CallCount { get; private set; }

        public Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct)
        {
            CallCount++;
            return Task.FromException<ArtworkResult>(new HttpRequestException("simulated provider outage"));
        }
    }

    private sealed class StaticTmdbClient(ArtworkResult result) : ITmdbClient
    {
        public int CallCount { get; private set; }

        public Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingTmdbClient(ArtworkResult result) : ITmdbClient
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }

        public async Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct)
        {
            CallCount++;
            Started.TrySetResult();
            await completion.Task.WaitAsync(ct);
            return result;
        }

        public void Complete() => completion.TrySetResult();
    }

    private sealed class UnusedIgdbClient : IIgdbClient
    {
        public Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct) => Unexpected<ArtworkResult>();
        public Task<long> GetMaximumGameIdAsync(CancellationToken ct) => Unexpected<long>();
        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct) => Unexpected<IReadOnlyList<IgdbGameType>>();
        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct) => Unexpected<IReadOnlyList<IgdbGame>>();

        private static Task<T> Unexpected<T>() => Task.FromException<T>(new InvalidOperationException("The TMDB test must not call IGDB."));
    }
}
