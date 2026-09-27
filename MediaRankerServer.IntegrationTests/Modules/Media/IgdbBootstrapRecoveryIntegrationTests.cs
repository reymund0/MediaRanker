// Opt-in integration coverage for the resumable IGDB bootstrap recovery paths.
//
// Uses only the test fixture database and recording IGDB clients.

using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

/// <summary>
/// Focused real-PostgreSQL recovery checks for bootstrap admission and the lease
/// boundary. Normal integration runs do not execute these tests. Set
/// MEDIARANKER_IGDB_RECOVERY_TESTS=1 and filter this class explicitly.
/// </summary>
public sealed class IgdbBootstrapRecoveryIntegrationTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private static readonly IReadOnlySet<string> SupportedTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Main game", "Remake", "Remaster" };

    [Fact]
    public async Task AdmissionBudget_RetainsStaging_AndReplayDoesNotRenewArtworkExpiry()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        db.Set<IgdbImport>().AddRange(
            Staged(501, now.AddHours(-2), cover: "cover-501"),
            Staged(502, now.AddHours(-2), cover: "cover-502"),
            Staged(503, now.AddHours(-2), cover: "cover-503"));
        await db.SaveChangesAsync();

        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        using var session = NewSession(maxAdmissionRows: 1, maxAdmissionBatches: 1);
        var result = await provider.LoadEligibleGamesAsync(lease, SupportedTypes, now,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), session.CreateUnit(WorkLimits(1, 1, 1)),
            CancellationToken.None);

        result.LoadedRows.Should().Be(1);
        result.Batches.Should().Be(1);
        result.Completed.Should().BeFalse();
        result.RemainingRows.Should().BeGreaterThan(0);
        (await db.Set<IgdbImport>().CountAsync()).Should().Be(3, "admission must retain staging for replay");

        var admitted = await db.Set<MediaCover>().AsNoTracking().SingleAsync(x => x.LookupId == "501");
        var originalExpiry = admitted.ExpiresAt;
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);

        db.ChangeTracker.Clear();
        var replayLease = (await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(2), CancellationToken.None))!;
        using var replaySession = NewSession(maxAdmissionRows: 10, maxAdmissionBatches: 2);
        var replay = await provider.LoadEligibleGamesAsync(replayLease, SupportedTypes, DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), replaySession.CreateUnit(WorkLimits(2, 10, 2)),
            CancellationToken.None);

        replay.LoadedRows.Should().Be(2);
        var replayed = await db.Set<MediaCover>().AsNoTracking().SingleAsync(x => x.LookupId == "501");
        replayed.ExpiresAt.Should().Be(originalExpiry, "a local staging replay cannot renew a completed cover");
        await provider.ReleaseLeaseAsync(replayLease, CancellationToken.None);
    }

    [Fact]
    public async Task CompletedBootstrapWithActiveIncrementalWindow_IsAdmissionOnlyAndDoesNotSendHttp()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        var before = now.AddMinutes(-1);
        db.Set<IgdbImportState>().Add(new IgdbImportState
        {
            BootstrapCompleted = true,
            RunIsBootstrap = false,
            RunUpdatedAfter = now.AddHours(-1),
            RunUpdatedBefore = before,
            LastCommittedId = 733,
            Version = 0
        });
        db.Set<IgdbImport>().Add(Staged(601, now.AddHours(-2), cover: "window-cover"));
        await db.SaveChangesAsync();

        var recording = new RecordingIgdbClient();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        using var session = NewSession();
        var service = CreateService(recording, provider);
        var result = await service.ImportAsync(session.CreateUnit(WorkLimits(3, 20, 10)), bootstrap: true);

        recording.Calls.Should().BeEmpty("a completed bootstrap with an active incremental window is admission-only");
        result.HttpAttempts.Should().Be(0);
        result.IncrementalPending.Should().BeTrue();
        result.AdmissionRows.Should().BeGreaterThan(0);
        var state = await db.Set<IgdbImportState>().AsNoTracking().SingleAsync();
        state.RunIsBootstrap.Should().BeFalse();
        state.RunUpdatedBefore.Should().BeCloseTo(before, TimeSpan.FromMicroseconds(1));
        state.LastCommittedId.Should().Be(733);
    }

    [Fact]
    public async Task ProviderFailure_PreDrainsStagingBeforeAnyProviderRequest()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        db.Set<IgdbImport>().Add(Staged(701, now.AddHours(-2), cover: "before-failure"));
        await db.SaveChangesAsync();

        var recording = new RecordingIgdbClient { Failure = new ProviderRequestException("fixture_unavailable") };
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        using var session = NewSession();
        var result = await CreateService(recording, provider)
            .ImportAsync(session.CreateUnit(WorkLimits(2, 10, 10)), bootstrap: true);

        result.StopReason.Should().Be(ImportStopReason.ProviderFailure);
        result.AdmissionRows.Should().Be(1);
        (await db.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb)).Should().Be(1);
        (await db.Set<IgdbImport>().CountAsync()).Should().Be(1, "pre-drain never deletes staging");
        recording.Calls.Should().Contain("game_types");
    }

    [Fact]
    public async Task BusyLease_ReturnsWithoutSendingHttp()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var busyUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        db.Set<IgdbImportState>().Add(new IgdbImportState
        {
            ClaimedUntil = busyUntil,
            ClaimToken = Guid.NewGuid(),
            Version = 9
        });
        await db.SaveChangesAsync();

        var recording = new RecordingIgdbClient();
        using var session = NewSession();
        var result = await CreateService(recording,
                new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance))
            .ImportAsync(session.CreateUnit(WorkLimits(2, 10, 10)), bootstrap: true);

        result.StopReason.Should().Be(ImportStopReason.LeaseBusy);
        result.LeaseAcquired.Should().BeFalse();
        result.HttpAttempts.Should().Be(0);
        result.LeaseBusyUntil.Should().NotBeNull();
        recording.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task StaleOwner_CannotCompleteOrReleaseTheReplacementLease()
    {
        if (!Enabled()) return;

        using var scopeA = Factory.Services.CreateScope();
        using var scopeB = Factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var providerA = new IgdbImportSqlProvider(dbA, NullLogger<IgdbImportSqlProvider>.Instance);
        var providerB = new IgdbImportSqlProvider(dbB, NullLogger<IgdbImportSqlProvider>.Instance);
        var first = (await providerA.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(100), CancellationToken.None))!;
        await dbA.Database.ExecuteSqlRawAsync("UPDATE igdb_import_state SET claimed_until = clock_timestamp() - interval '1 second'");
        var second = (await providerB.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), CancellationToken.None))!;

        Func<Task> completeStaleLease = () => providerA.CompleteRunAsync(
            first, DateTimeOffset.UtcNow, CancellationToken.None);
        await completeStaleLease.Should().ThrowAsync<InvalidOperationException>();
        await providerA.ReleaseLeaseAsync(first, CancellationToken.None);

        var state = await dbB.Set<IgdbImportState>().AsNoTracking().SingleAsync();
        state.ClaimToken.Should().Be(second.Token, "a stale owner may not clear the replacement claim");
        state.BootstrapCompleted.Should().BeFalse();
        await providerB.ReleaseLeaseAsync(second, CancellationToken.None);
    }

    [Fact]
    public async Task CommitPageAfterLeaseExpiry_RollsBackStagingAndCursor()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var now = DateTimeOffset.UtcNow;
        var lease = (await provider.TryAcquireLeaseAsync(now, TimeSpan.FromMilliseconds(250), CancellationToken.None))!;
        await provider.StartRunAsync(lease, bootstrap: true, maximumId: 9_999, null, null, now, CancellationToken.None);

        // Hold the state row so CommitPageAsync cannot inspect the lease until its
        // database-clock expiry. This is deterministic and keeps all writes in the
        // disposable test transaction.
        await using var blocker = new Npgsql.NpgsqlConnection(db.Database.GetConnectionString());
        await blocker.OpenAsync();
        await using var blockerTransaction = await blocker.BeginTransactionAsync();
        await using (var lockCommand = new Npgsql.NpgsqlCommand(
                         "SELECT id FROM igdb_import_state WHERE id = 1 FOR UPDATE", blocker, blockerTransaction))
        {
            await lockCommand.ExecuteScalarAsync();
        }

        var commit = provider.CommitPageAsync(lease, [Staged(901, now, cover: "lease-expired")], 901, CancellationToken.None);
        await Task.Delay(400);
        await blockerTransaction.RollbackAsync();

        Func<Task> observeCommit = async () => await commit;
        await observeCommit.Should().ThrowAsync<InvalidOperationException>();
        (await db.Set<IgdbImport>().CountAsync()).Should().Be(0);
        (await db.Set<IgdbImportState>().AsNoTracking().SingleAsync()).LastCommittedId.Should().Be(0);
    }

    [Fact]
    public async Task FresherOnDemandCover_IsPreservedWhenOlderStagingIsAdmitted()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var staleFetch = DateTimeOffset.UtcNow.AddHours(-3);
        var freshCover = staleFetch.AddHours(1);
        var media = new MediaEntity
        {
            Title = "Fresh title",
            ExternalId = "1001",
            ExternalSource = MediaExternalSource.Igdb,
            MediaType = "VideoGame",
            ReleaseDate = DateOnly.FromDateTime(staleFetch.AddDays(-1).UtcDateTime),
            CreatedAt = freshCover,
            UpdatedAt = freshCover
        };
        var cover = new MediaCover
        {
            Provider = ArtworkProvider.Igdb,
            LookupKind = CoverLookupKind.IgdbGame,
            LookupId = "1001",
            ProviderItemId = "1001",
            ImagePath = "fresh-on-demand",
            Outcome = CoverOutcome.Ready,
            CheckedAt = freshCover,
            ExpiresAt = freshCover.AddDays(30),
            Version = 7,
            CreatedAt = freshCover,
            UpdatedAt = freshCover
        };
        media.Cover = cover;
        db.Set<MediaEntity>().Add(media);
        db.Set<IgdbImport>().Add(Staged(1001, staleFetch, cover: "old-staged"));
        await db.SaveChangesAsync();

        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        using var session = NewSession();
        var result = await provider.LoadEligibleGamesAsync(lease, SupportedTypes, DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), session.CreateUnit(WorkLimits(2, 10, 10)),
            CancellationToken.None);

        result.LoadedRows.Should().Be(0, "the fresh canonical cover is already newer than staging");
        var unchanged = await db.Set<MediaCover>().AsNoTracking().SingleAsync(x => x.LookupId == "1001");
        unchanged.ImagePath.Should().Be("fresh-on-demand");
        unchanged.Version.Should().Be(7);
        unchanged.ExpiresAt.Should().BeCloseTo(freshCover.AddDays(30), TimeSpan.FromMicroseconds(1));
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task BlockedCommitPage_UsesStatementTimeoutRollsBackAndAllowsLeaseRecovery()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var fixtureDb = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var connectionString = fixtureDb.Database.GetConnectionString()!;
        await using var db = NewContext(connectionString);
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance,
            igdbOptions: Options.Create(new IgdbOptions { MaxStatementSeconds = 1 }));
        var lease = (await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(2), CancellationToken.None))!;

        await using var blockerDb = NewContext(connectionString);
        await using var blocker = await blockerDb.Database.BeginTransactionAsync();
        await blockerDb.Database.ExecuteSqlRawAsync(
            $"UPDATE igdb_import_state SET version = version WHERE id = {IgdbImportState.SingletonId}");

        var startedAt = Stopwatch.GetTimestamp();
        Func<Task> commitPage = async () =>
            await provider.CommitPageAsync(lease, [Staged(1251, DateTimeOffset.UtcNow)], 1251, CancellationToken.None);
        var failure = await commitPage.Should().ThrowAsync<InvalidOperationException>();
        Stopwatch.GetElapsedTime(startedAt).Should().BeLessThan(TimeSpan.FromSeconds(5));
        failure.Which.InnerException.Should().BeOfType<NpgsqlException>();
        failure.Which.GetBaseException().Should().BeOfType<TimeoutException>();

        // The timeout aborts only the page transaction; its lease and cursor remain usable.
        db.Database.CurrentTransaction.Should().BeNull();
        db.Database.GetCommandTimeout().Should().BeNull();
        await blocker.RollbackAsync();
        (await db.Set<IgdbImport>().AsNoTracking().AnyAsync(x => x.IgdbGameId == 1251)).Should().BeFalse();
        var state = await db.Set<IgdbImportState>().AsNoTracking().SingleAsync();
        state.LastCommittedId.Should().Be(0);
        state.ClaimToken.Should().Be(lease.Token);

        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
        var recoveredLease = await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(2), CancellationToken.None);
        recoveredLease.Should().NotBeNull();
        await provider.ReleaseLeaseAsync(recoveredLease!, CancellationToken.None);
    }

    [Fact]
    public async Task FinalPageAdmissionRetry_CompletesBootstrapAndPreservesTheFresherCover()
    {
        if (!Enabled()) return;

        using var seedScope = Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var race = new CanonicalInsertRaceInterceptor(pauseOnlyWhenAddingMedia: true);
        await using var raceDb = NewContext(seedDb.Database.GetConnectionString()!, race);
        var raceProvider = new IgdbImportSqlProvider(raceDb, NullLogger<IgdbImportSqlProvider>.Instance,
            Factory.Services.GetRequiredService<IServiceScopeFactory>());
        var client = new RecordingIgdbClient
        {
            MaximumId = 1051,
            Pages = [[new IgdbGame(1051, "On-demand title", DateTimeOffset.UtcNow.AddDays(-2), 0, null,
                "staged-cover", DateTimeOffset.UtcNow.AddMinutes(-1))]]
        };
        using var session = NewSession();
        using var unit = session.CreateUnit(WorkLimits(2, 10, 10));
        var importing = CreateService(client, raceProvider).ImportAsync(unit, bootstrap: true);

        await race.MediaLookupReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using (var competingDb = NewContext(seedDb.Database.GetConnectionString()!))
        {
            var completionAt = DateTimeOffset.UtcNow;
            var competingMedia = new MediaEntity
            {
                Title = "On-demand title",
                ExternalId = "1051",
                ExternalSource = MediaExternalSource.Igdb,
                MediaType = "VideoGame",
                ReleaseDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(-2).UtcDateTime),
                CreatedAt = completionAt,
                UpdatedAt = completionAt,
                Cover = new MediaCover
                {
                    Provider = ArtworkProvider.Igdb,
                    LookupKind = CoverLookupKind.IgdbGame,
                    LookupId = "1051",
                    ProviderItemId = "1051",
                    ImagePath = "fresh-race-cover",
                    Outcome = CoverOutcome.Ready,
                    CheckedAt = completionAt,
                    ExpiresAt = completionAt.AddDays(30),
                    Version = 2,
                    CreatedAt = completionAt,
                    UpdatedAt = completionAt
                }
            };
            competingDb.Set<MediaEntity>().Add(competingMedia);
            await competingDb.SaveChangesAsync();
        }

        race.ReleaseMediaLookup();
        var result = await importing;

        result.StopReason.Should().Be(ImportStopReason.Completed,
            "the original context must reread the lease after the final-page admission retry");
        result.PagesCommitted.Should().Be(1);
        result.RowsCommitted.Should().Be(1);
        result.AdmissionRetryCount.Should().Be(1);
        result.RunCompleted.Should().BeTrue();
        result.CatalogReady.Should().BeTrue();
        (await seedDb.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "1051"))
            .Should().Be(1);
        var stagedAtAfterRun = await seedDb.Set<IgdbImport>().AsNoTracking()
            .Where(x => x.IgdbGameId == 1051).Select(x => x.FetchedAt).SingleAsync();
        var preserved = await seedDb.Set<MediaCover>().AsNoTracking().SingleAsync(x => x.LookupId == "1051");
        preserved.ImagePath.Should().Be("fresh-race-cover");
        preserved.CheckedAt.Should().BeOnOrAfter(stagedAtAfterRun);
        (await seedDb.Set<IgdbImportState>().AsNoTracking().SingleAsync()).BootstrapCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RetryWithoutScopeFactory_ClearsFailedBatchTrackingAndRetriesOnce()
    {
        if (!Enabled()) return;

        using var seedScope = Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        seedDb.Set<IgdbImport>().Add(Staged(1061, now.AddHours(-2), cover: "in-place-retry"));
        await seedDb.SaveChangesAsync();

        var seedProvider = new IgdbImportSqlProvider(seedDb, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await seedProvider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var saveFailure = new FailFirstSaveInterceptor();
        await using var retryDb = NewContext(seedDb.Database.GetConnectionString()!, saveFailure);
        var retryProvider = new IgdbImportSqlProvider(retryDb, NullLogger<IgdbImportSqlProvider>.Instance);
        using var session = NewSession();
        using var unit = session.CreateUnit(WorkLimits(1, 10, 10));

        var result = await retryProvider.LoadEligibleGamesAsync(lease, SupportedTypes, now,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), unit, CancellationToken.None);

        result.Blocked.Should().BeFalse();
        result.RetryCount.Should().Be(1);
        result.LoadedRows.Should().Be(1);
        saveFailure.SaveAttempts.Should().Be(2);
        (await seedDb.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "1061"))
            .Should().Be(1);
        await seedProvider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task RetryExhaustion_ReturnsAdmissionBlockedAndRetainsTheStagingPrefix()
    {
        if (!Enabled()) return;

        using var seedScope = Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        seedDb.Set<IgdbImport>().Add(Staged(1071, now.AddHours(-2), cover: "retry-exhausted"));
        await seedDb.SaveChangesAsync();

        var seedProvider = new IgdbImportSqlProvider(seedDb, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await seedProvider.TryAcquireLeaseAsync(now, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        var createdContexts = new List<Guid>();
        using var retryServices = new ServiceCollection().AddLogging()
            .AddScoped(_ =>
            {
                var context = NewContext(seedDb.Database.GetConnectionString()!, new AlwaysConcurrencySaveInterceptor());
                createdContexts.Add(context.ContextId.InstanceId);
                return context;
            }).AddScoped<IIgdbImportProvider, IgdbImportSqlProvider>().BuildServiceProvider();
        using var firstScope = retryServices.CreateScope();
        var failingProvider = firstScope.ServiceProvider.GetRequiredService<IIgdbImportProvider>();
        using var session = NewSession();
        var result = await failingProvider.LoadEligibleGamesAsync(lease, SupportedTypes, now,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), session.CreateUnit(WorkLimits(2, 10, 10)),
            CancellationToken.None);

        result.Blocked.Should().BeTrue();
        result.LoadedRows.Should().Be(0);
        result.BlockReason.Should().Be("batch_failed");
        result.RetryCount.Should().Be(2);
        result.BlockBatch.Should().Be("1071..1071; count=1; category=DbUpdateConcurrencyException");
        createdContexts.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        (await seedDb.Set<IgdbImport>().CountAsync()).Should().Be(1);
        (await seedDb.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb)).Should().Be(0);
        await seedProvider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactSessionAdmissionCapCanFinishACompletedScan(bool hasFutureRelease)
    {
        if (!Enabled()) return;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Set<IgdbImportState>().Add(new IgdbImportState { BootstrapCompleted = true });
        db.Set<IgdbImport>().Add(Staged(1081, DateTimeOffset.UtcNow.AddHours(-1), "exact-cap"));
        if (hasFutureRelease)
        {
            var futureRelease = Staged(1082, DateTimeOffset.UtcNow.AddHours(-1), "future-release");
            futureRelease.FirstReleaseDate = DateTimeOffset.UtcNow.AddDays(2);
            db.Set<IgdbImport>().Add(futureRelease);
        }
        await db.SaveChangesAsync();
        var client = new RecordingIgdbClient();
        using var session = NewSession(maxAdmissionRows: 1);
        using var unit = session.CreateUnit(WorkLimits(1, 10, 1));
        var result = await CreateService(client, new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance))
            .ImportAsync(unit, bootstrap: true);
        result.CatalogReady.Should().BeTrue();
        result.EligibleGamesLoaded.Should().Be(1);
        result.AdmissionRemaining.Should().Be(0);
        session.AdmissionRows.Should().Be(1);
        client.Calls.Should().BeEmpty();
        if (hasFutureRelease)
        {
            (await db.Set<IgdbImport>().AnyAsync(x => x.IgdbGameId == 1082)).Should().BeTrue();
            (await db.Set<MediaEntity>().AnyAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "1082"))
                .Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiryDuringAdmissionOrFreshRetryPropagatesLeaseLossAndRollsBack(bool forceRetry)
    {
        if (!Enabled()) return;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Set<IgdbImport>().Add(Staged(1085, DateTimeOffset.UtcNow.AddHours(-1), "expiry"));
        await db.SaveChangesAsync();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(500), CancellationToken.None))!;
        await using var slowDb = NewContext(db.Database.GetConnectionString()!, new ExpireAdmissionInterceptor(forceRetry));
        var slowProvider = new IgdbImportSqlProvider(slowDb, NullLogger<IgdbImportSqlProvider>.Instance,
            Factory.Services.GetRequiredService<IServiceScopeFactory>());
        using var session = NewSession();
        using var unit = session.CreateUnit(WorkLimits(1, 10, 10));
        var run = () => slowProvider.LoadEligibleGamesAsync(lease, SupportedTypes, DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), unit, CancellationToken.None);
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("IGDB import lease was lost*");
        (await db.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb)).Should().Be(0);
        (await db.Set<MediaCover>().CountAsync(x => x.Provider == ArtworkProvider.Igdb)).Should().Be(0);
        (await db.Set<IgdbImport>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CoverVersionRace_RereadsInFreshContextAndPreservesNewerCompletion()
    {
        if (!Enabled()) return;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var fetched = DateTimeOffset.UtcNow.AddHours(-2);
        db.Set<IgdbImport>().Add(Staged(1091, fetched, "initial"));
        await db.SaveChangesAsync();
        var provider = new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance);
        var lease = (await provider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), CancellationToken.None))!;
        await provider.LoadEligibleGamesAsync(lease, SupportedTypes, DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), CancellationToken.None);
        await db.Set<IgdbImport>().Where(x => x.IgdbGameId == 1091).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.FetchedAt, fetched.AddHours(1)).SetProperty(x => x.CoverImageId, "staged-change"));

        var barrier = new CanonicalInsertRaceInterceptor();
        await using var racedDb = NewContext(db.Database.GetConnectionString()!, barrier);
        var racedProvider = new IgdbImportSqlProvider(racedDb, NullLogger<IgdbImportSqlProvider>.Instance,
            Factory.Services.GetRequiredService<IServiceScopeFactory>());
        using var session = NewSession();
        using var unit = session.CreateUnit(WorkLimits(1, 10, 10));
        var admission = racedProvider.LoadEligibleGamesAsync(lease, SupportedTypes, DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30), TimeSpan.FromDays(7), unit, CancellationToken.None);
        await barrier.MediaLookupReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var completion = DateTimeOffset.UtcNow;
        await db.Set<MediaCover>().Where(x => x.LookupId == "1091").ExecuteUpdateAsync(s => s
            .SetProperty(x => x.ImagePath, "on-demand-newer")
            .SetProperty(x => x.CheckedAt, completion).SetProperty(x => x.ExpiresAt, completion.AddDays(30))
            .SetProperty(x => x.Version, x => x.Version + 1));
        barrier.ReleaseMediaLookup();
        var result = await admission;
        result.Blocked.Should().BeFalse();
        result.RetryCount.Should().Be(1);
        var cover = await db.Set<MediaCover>().AsNoTracking().SingleAsync(x => x.LookupId == "1091");
        cover.ImagePath.Should().Be("on-demand-newer");
        cover.ExpiresAt.Should().BeCloseTo(completion.AddDays(30), TimeSpan.FromMicroseconds(1));
        await provider.ReleaseLeaseAsync(lease, CancellationToken.None);
    }

    [Fact]
    public async Task EligibilityStatementTimeoutStopsAdmissionBeforeHttpAndRetainsStaging()
    {
        if (!Enabled()) return;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        db.Set<IgdbImport>().Add(Staged(1101, DateTimeOffset.UtcNow.AddHours(-1), "blocked-query"));
        await db.SaveChangesAsync();
        await using var blocker = NewContext(db.Database.GetConnectionString()!);
        await using var blockedTransaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("LOCK TABLE igdb_imports IN ACCESS EXCLUSIVE MODE");
        await using var importDb = NewContext(db.Database.GetConnectionString()!);
        var provider = new IgdbImportSqlProvider(importDb, NullLogger<IgdbImportSqlProvider>.Instance,
            igdbOptions: Options.Create(new IgdbOptions { MaxStatementSeconds = 1 }));
        var client = new RecordingIgdbClient();
        using var session = NewSession();
        using var unit = session.CreateUnit(WorkLimits(1, 10, 10));

        var result = await CreateService(client, provider).ImportAsync(unit, bootstrap: false)
            .WaitAsync(TimeSpan.FromSeconds(10));

        result.StopReason.Should().Be(ImportStopReason.AdmissionBlocked);
        result.CatalogReady.Should().BeFalse();
        client.Calls.Should().BeEmpty();
        await blockedTransaction.RollbackAsync();
        (await db.Set<IgdbImport>().CountAsync()).Should().Be(1);
        (await db.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb)).Should().Be(0);
    }

    [Fact]
    public async Task NonRetryableAdmissionFailure_StopsBeforeHttpAndCursorAdvance()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var now = DateTimeOffset.UtcNow;
        db.Set<IgdbImport>().Add(Staged(1101, now.AddHours(-2), cover: "nonretryable"));
        await db.SaveChangesAsync();

        // The SaveChanges interceptor is a deterministic non-retryable database
        // error. It exercises the admission prefix boundary without touching
        // production schema or the real provider.
        var recording = new RecordingIgdbClient();
        await using var failingDb = NewContext(db.Database.GetConnectionString()!, new AlwaysNonRetryableSaveInterceptor());
        var provider = new IgdbImportSqlProvider(failingDb, NullLogger<IgdbImportSqlProvider>.Instance);
        using var session = NewSession(maxAdmissionRows: 1, maxAdmissionBatches: 1);
        var result = await CreateService(recording, provider)
            .ImportAsync(session.CreateUnit(WorkLimits(1, 10, 1)), bootstrap: true);

        // This assertion intentionally exposes the required fail-stop contract;
        // it must be adjusted if the implementation reports a more specific
        // non-retryable stop reason in the final API.
        result.StopReason.Should().Be(ImportStopReason.AdmissionBlocked);
        result.HttpAttempts.Should().Be(0);
        recording.Calls.Should().BeEmpty();
        (await db.Set<IgdbImport>().CountAsync()).Should().Be(1);
        (await db.Set<IgdbImportState>().AsNoTracking().SingleAsync()).LastCommittedId.Should().Be(0);
    }

    [Fact]
    public async Task BootstrapMetadataImport_UsesOnlyIgdbAndDoesNotCreateTmdbDemand()
    {
        if (!Enabled()) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var importedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var recording = new RecordingIgdbClient
        {
            MaximumId = 1200,
            Types = [new IgdbGameType(0, "Main game")],
            Pages = [[new IgdbGame(1200, "Metadata game", DateTimeOffset.UtcNow.AddDays(-2), 0, null,
                "metadata-cover", importedAt)]]
        };
        using var session = NewSession(maxAdmissionRows: 10, maxAdmissionBatches: 2);
        var result = await CreateService(recording,
                new IgdbImportSqlProvider(db, NullLogger<IgdbImportSqlProvider>.Instance))
            .ImportAsync(session.CreateUnit(WorkLimits(3, 20, 10)), bootstrap: true);

        result.RunCompleted.Should().BeTrue();
        recording.Calls.Should().Contain("game_types");
        recording.Calls.Should().Contain("maximum_id");
        recording.Calls.Should().Contain("games");
        recording.Calls.Should().NotContain("cover", "metadata import must reuse the staged IGDB cover reference");
        (await db.Set<MediaCover>().CountAsync(x => x.Provider == ArtworkProvider.Tmdb)).Should().Be(0);
        var importedGame = await db.Set<MediaEntity>().AsNoTracking()
            .SingleAsync(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId == "1200");
        var importedCover = await db.Set<MediaCover>().AsNoTracking()
            .SingleAsync(x => x.Provider == ArtworkProvider.Igdb && x.LookupKind == CoverLookupKind.IgdbGame
                && x.LookupId == "1200");
        importedCover.ImagePath.Should().Be("metadata-cover");
        importedCover.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        // Add a user-owned review so both authorized browse and review read models
        // exercise the cover produced by the actual metadata admission above.
        db.Reviews.Add(new Review
        {
            UserId = TestAuthHandler.DefaultUserId,
            TemplateId = -1,
            MediaId = importedGame.Id,
            OverallScore = 8,
            Fields = [new ReviewField { TemplateFieldId = -11, Value = 8 }]
        });
        await db.SaveChangesAsync();

        // Keep both outbound provider clients local and observable. Any missed cache
        // hit would be recorded and blocked before it could reach IGDB or Twitch.
        var providerHttpAttempts = new ConcurrentQueue<string>();
        using var endpointFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<IgdbClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new RecordingProviderHttpHandler(providerHttpAttempts));
            services.AddHttpClient(MediaProviderServiceCollectionExtensions.IgdbTwitchClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new RecordingProviderHttpHandler(providerHttpAttempts));
        }));
        using var endpointClient = endpointFactory.CreateClient();

        var browseResponse = await endpointClient.GetAsync(
            "/api/media?mediaType=VideoGame&searchField=title&searchTerm=Metadata%20game&page=0&pageSize=10&sortField=title&sortDirection=asc");
        browseResponse.IsSuccessStatusCode.Should().BeTrue();
        var browsePage = await browseResponse.Content.ReadFromJsonAsync<PageResult<MediaDto>>();
        var browsedGame = browsePage!.Items.Should().ContainSingle().Which;
        browsedGame.Id.Should().Be(importedGame.Id);
        browsedGame.Title.Should().Be("Metadata game");
        browsedGame.CoverImageUrl.Should().Be("https://images.igdb.com/igdb/image/upload/t_cover_big/metadata-cover.jpg");
        browsedGame.CoverStatus.Should().Be("ready");

        var reviewResponse = await endpointClient.GetAsync("/api/reviews/byMediaType/VideoGame");
        reviewResponse.IsSuccessStatusCode.Should().BeTrue();
        var reviews = await reviewResponse.Content.ReadFromJsonAsync<List<ReviewDto>>();
        var gameReview = reviews!.Should().ContainSingle(review => review.MediaId == importedGame.Id).Which;
        gameReview.MediaTitle.Should().Be("Metadata game");
        gameReview.MediaCoverImageUrl.Should().Be("https://images.igdb.com/igdb/image/upload/t_cover_big/metadata-cover.jpg");
        gameReview.CoverStatus.Should().Be("ready");

        providerHttpAttempts.Should().BeEmpty("fresh imported covers must avoid IGDB discovery and Twitch token HTTP");
        (await db.Set<MediaCover>().CountAsync(x => x.Provider == ArtworkProvider.Tmdb)).Should().Be(0,
            "IGDB import and authorized reads must not create TMDB demand");
    }

    [Fact]
    public async Task ScheduledSession_ResumesPageBudgetAcrossFreshSqlScopes()
    {
        if (!Enabled()) return;

        using var fixtureScope = Factory.Services.CreateScope();
        var fixtureDb = fixtureScope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var client = new RecordingIgdbClient
        {
            MaximumId = 1300,
            Pages =
            [
                [new IgdbGame(1201, "Scheduled game 1201", DateTimeOffset.UtcNow.AddDays(-2), 0, null,
                    "scheduled-cover-1201", DateTimeOffset.UtcNow.AddMinutes(-1))],
                [new IgdbGame(1202, "Scheduled game 1202", DateTimeOffset.UtcNow.AddDays(-2), 0, null,
                    "scheduled-cover-1202", DateTimeOffset.UtcNow.AddMinutes(-1))]
            ]
        };
        var contextObserver = new SavingContextObserver();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgreSQLContext>(options => options
            .UseNpgsql(fixtureDb.Database.GetConnectionString()!)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(contextObserver));
        services.AddSingleton<IIgdbClient>(client);
        services.AddScoped<IIgdbImportProvider, IgdbImportSqlProvider>();
        services.AddScoped<IgdbImportService>();
        services.Configure<IgdbOptions>(options =>
        {
            options.ImportEnabled = true;
            options.ClientId = "fixture-client";
            options.ClientSecret = "fixture-secret";
            options.PageSize = 1;
            options.PageBudget = 2;
            options.WorkUnitPageLimit = 1;
            options.WorkUnitHttpAttemptLimit = 10;
            options.WorkUnitAdmissionRowLimit = 10;
            options.WorkUnitAdmissionBatchLimit = 1;
            options.WorkUnitSeconds = 30;
            options.ScheduledSessionHttpAttemptLimit = 20;
            options.ScheduledSessionAdmissionRowLimit = 10;
            options.ScheduledSessionMinutes = 2;
            options.LeaseSeconds = 120;
        });
        services.Configure<ArtworkOptions>(options =>
        {
            options.PositiveCacheDays = 30;
            options.NegativeCacheDays = 7;
        });

        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var importScope = serviceProvider.CreateScope();
        var result = await importScope.ServiceProvider.GetRequiredService<IgdbImportService>().ImportAsync();

        result.PagesCommitted.Should().Be(2);
        result.RowsCommitted.Should().Be(2);
        result.EligibleGamesLoaded.Should().Be(2);
        result.RunMaximumId.Should().Be(1300);
        result.DurableCursor.Should().Be(1202);
        result.StopReason.Should().Be(ImportStopReason.UnitPageLimit);
        result.RunCompleted.Should().BeFalse("the two-page allowance ends before a terminal empty page");
        result.CatalogReady.Should().BeFalse();
        result.AdmissionRows.Should().Be(2);
        result.AdmissionBatches.Should().Be(2);
        client.Calls.Count(x => x == "games").Should().Be(2, "the total scheduled page cap cannot renew between scopes");
        contextObserver.Contexts.Should().HaveCountGreaterThan(1, "each work unit must use a fresh PostgreSQL context");
        (await fixtureDb.Set<MediaEntity>().CountAsync(x => x.ExternalSource == MediaExternalSource.Igdb)).Should().Be(2);
        var state = await fixtureDb.Set<IgdbImportState>().AsNoTracking().SingleAsync();
        state.RunIsBootstrap.Should().BeTrue();
        state.BootstrapCompleted.Should().BeFalse();
        state.LastCommittedId.Should().Be(1202);
    }

    private static PostgreSQLContext NewContext(string connectionString, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptors)
            .Options;
        return new PostgreSQLContext(options);
    }

    private static bool Enabled() => string.Equals(
        Environment.GetEnvironmentVariable("MEDIARANKER_IGDB_RECOVERY_TESTS"), "1", StringComparison.Ordinal);

    private static ImportBudgetSession NewSession(int maxAdmissionRows = 50, int maxAdmissionBatches = 4) =>
        new(new ImportSessionLimits(20, maxAdmissionRows, TimeSpan.FromSeconds(15)));

    private static ImportWorkUnitLimits WorkLimits(int maxPages, int maxHttp, int maxAdmissionRows) =>
        new(maxPages, maxHttp, maxAdmissionRows, 4, TimeSpan.FromSeconds(15));

    private static IgdbImport Staged(long id, DateTimeOffset fetchedAt, string? cover = null,
        string type = "Main game", string? name = null) => new()
    {
        IgdbGameId = id,
        Name = name ?? $"Fixture game {id}",
        GameTypeName = type,
        FirstReleaseDate = fetchedAt.AddDays(-1),
        CoverImageId = cover,
        ProviderUpdatedAt = fetchedAt,
        FetchedAt = fetchedAt,
        CreatedAt = fetchedAt,
        UpdatedAt = fetchedAt
    };

    private static IgdbImportService CreateService(IIgdbClient client, IIgdbImportProvider provider)
    {
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            LeaseSeconds = 120,
            PageSize = 100,
            IncrementalOverlapMinutes = 10
        };
        return new IgdbImportService(client, provider, Options.Create(options),
            Options.Create(new ArtworkOptions { PositiveCacheDays = 30, NegativeCacheDays = 7 }),
            NullLogger<IgdbImportService>.Instance);
    }

    private sealed class RecordingIgdbClient : IIgdbClient
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public ProviderRequestException? Failure { get; init; }
        public long MaximumId { get; init; } = 1;
        public IReadOnlyList<IgdbGameType> Types { get; init; } = [new IgdbGameType(0, "Main game")];
        public IReadOnlyList<IReadOnlyList<IgdbGame>> Pages { get; init; } = [];
        private int pageIndex;

        public Task<long> GetMaximumGameIdAsync(ImportWorkUnitBudget budget, CancellationToken ct)
        {
            if (!budget.TryReserveHttp("maximum_id", out var reason)) throw new ImportBudgetExceededException(reason);
            return GetMaximumGameIdAsync(ct);
        }

        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(ImportWorkUnitBudget budget, CancellationToken ct)
        {
            if (!budget.TryReserveHttp("game_types", out var reason)) throw new ImportBudgetExceededException(reason);
            return GetGameTypesAsync(ct);
        }

        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, ImportWorkUnitBudget budget, CancellationToken ct)
        {
            if (!budget.TryReservePage(out var reason)) throw new ImportBudgetExceededException(reason);
            if (!budget.TryReserveHttp("games", out reason)) throw new ImportBudgetExceededException(reason);
            return GetGamesAsync(query, ct);
        }

        public Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct)
        {
            Calls.Enqueue("cover");
            ThrowIfConfigured();
            return Task.FromResult(new ArtworkResult(gameId, "fixture-cover"));
        }

        public Task<long> GetMaximumGameIdAsync(CancellationToken ct)
        {
            Calls.Enqueue("maximum_id");
            ThrowIfConfigured();
            return Task.FromResult(MaximumId);
        }

        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct)
        {
            Calls.Enqueue("game_types");
            ThrowIfConfigured();
            return Task.FromResult(Types);
        }

        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct)
        {
            Calls.Enqueue("games");
            ThrowIfConfigured();
            var current = Interlocked.Increment(ref pageIndex) - 1;
            return Task.FromResult(current < Pages.Count ? Pages[current] : (IReadOnlyList<IgdbGame>)[]);
        }

        private void ThrowIfConfigured()
        {
            if (Failure is not null) throw Failure;
        }
    }

    private sealed class RecordingProviderHttpHandler(ConcurrentQueue<string> attempts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            attempts.Enqueue($"{request.Method} {request.RequestUri}");
            return Task.FromException<HttpResponseMessage>(
                new InvalidOperationException("Provider HTTP is blocked by the integration fixture."));
        }
    }

    private sealed class CanonicalInsertRaceInterceptor(bool pauseOnlyWhenAddingMedia = false) : SaveChangesInterceptor
    {
        private int mediaLookupCount;
        private readonly TaskCompletionSource<bool> release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> MediaLookupReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseMediaLookup() => release.TrySetResult(true);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (pauseOnlyWhenAddingMedia
                && !eventData.Context!.ChangeTracker.Entries<MediaEntity>().Any(x => x.State == EntityState.Added))
                return result;

            // Pause after all admission reads, immediately before the first write.
            // The competing insert then deterministically forces unique recovery.
            if (Interlocked.Increment(ref mediaLookupCount) == 1)
            {
                MediaLookupReached.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class AlwaysConcurrencySaveInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result) =>
            throw new DbUpdateConcurrencyException("fixture concurrency conflict");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateConcurrencyException("fixture concurrency conflict"));
    }

    private sealed class ExpireAdmissionInterceptor(bool forceRetry) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await Task.Delay(650, cancellationToken);
            if (forceRetry) throw new DbUpdateConcurrencyException("fixture conflict after lease expiry");
            return result;
        }
    }

    private sealed class AlwaysNonRetryableSaveInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result) =>
            throw new InvalidOperationException("fixture non-retryable admission prefix");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new InvalidOperationException("fixture non-retryable admission prefix"));
    }

    private sealed class SavingContextObserver : SaveChangesInterceptor
    {
        public ConcurrentDictionary<DbContext, byte> Contexts { get; } = new();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context)
                Contexts.TryAdd(context, 0);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailFirstSaveInterceptor : SaveChangesInterceptor
    {
        private int saveAttempts;

        public int SaveAttempts => Volatile.Read(ref saveAttempts);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            return Interlocked.Increment(ref saveAttempts) == 1
                ? ValueTask.FromException<InterceptionResult<int>>(
                    new DbUpdateConcurrencyException("fixture first-attempt conflict"))
                : ValueTask.FromResult(result);
        }
    }
}
