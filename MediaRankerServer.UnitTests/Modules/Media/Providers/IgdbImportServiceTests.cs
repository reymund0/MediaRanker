using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media.Providers;

public class IgdbImportServiceTests
{
    [Fact]
    public async Task ImportAsync_StagesOnePageCommitsCursorAndAdmitsSupportedType()
    {
        var provider = new RecordingImportProvider();
        var client = new FakeIgdbClient(
            [new IgdbGameType(7, "Main game")],
            [new IgdbGame(10, "Released game", DateTimeOffset.UtcNow.AddDays(-1), 7, null, "cover_id", DateTimeOffset.UtcNow)]);
        var service = new IgdbImportService(client, provider,
            Options.Create(new IgdbOptions { ImportEnabled = true, ClientId = "id", ClientSecret = "secret", PageSize = 10 }),
            Options.Create(new ArtworkOptions()), NullLogger<IgdbImportService>.Instance);

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(1);
        result.RowsCommitted.Should().Be(1);
        result.EligibleGamesLoaded.Should().Be(1);
        result.RunCompleted.Should().BeTrue();
        result.LeaseAcquired.Should().BeTrue();
        result.StopReason.Should().Be(ImportStopReason.Completed);
        provider.Committed.Single().IgdbGameId.Should().Be(10);
        provider.Committed.Single().GameTypeName.Should().Be("Main game");
        provider.Committed.Single().CoverImageId.Should().Be("cover_id");
        provider.LastCommittedId.Should().Be(10);
        provider.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_WhenAlreadyLeased_DoesNotCallProviderHttp()
    {
        var client = new FakeIgdbClient([], []);
        var provider = new RecordingImportProvider { ReturnLease = false };
        var service = new IgdbImportService(client, provider,
            Options.Create(new IgdbOptions { ImportEnabled = true, ClientId = "id", ClientSecret = "secret" }),
            Options.Create(new ArtworkOptions()), NullLogger<IgdbImportService>.Instance);

        var result = await service.ImportAsync();

        result.LeaseAcquired.Should().BeFalse();
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_IncrementalRunKeepsFixedOverlapAndProcessesEqualTimestampPages()
    {
        var watermark = DateTimeOffset.UtcNow.AddHours(-1);
        var client = new FakeIgdbClient(
            [new IgdbGameType(7, "Main game")],
            [],
            query => query.AfterId switch
            {
                0 => [Game(11, watermark), Game(12, watermark)],
                12 => [Game(13, watermark), Game(14, watermark)],
                14 => [],
                _ => throw new InvalidOperationException($"Unexpected cursor {query.AfterId}")
            });
        var provider = new RecordingImportProvider();
        provider.State.BootstrapCompleted = true;
        provider.State.LastCompletedUpdatedAt = watermark;
        var service = CreateService(client, provider, new IgdbOptions { ImportEnabled = true, ClientId = "id", ClientSecret = "secret", PageSize = 2 });

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(2);
        result.RowsCommitted.Should().Be(4);
        result.EligibleGamesLoaded.Should().Be(4);
        result.RunCompleted.Should().BeTrue();
        result.LeaseAcquired.Should().BeTrue();
        result.StopReason.Should().Be(ImportStopReason.Completed);
        provider.Committed.Select(x => x.IgdbGameId).Should().Equal(11, 12, 13, 14);
        client.Queries.Select(x => x.AfterId).Should().Equal(0, 12, 14);
        client.Queries.Select(x => x.UpdatedAfter).Should().OnlyContain(x => x == client.Queries[0].UpdatedAfter);
        client.Queries.Select(x => x.UpdatedBefore).Should().OnlyContain(x => x == client.Queries[0].UpdatedBefore);
        client.Queries[0].UpdatedAfter.Should().NotBeNull();
        client.Queries[0].UpdatedAfter!.Value.Should().BeBefore(watermark);
    }

    [Fact]
    public async Task ImportAsync_ZeroChangePageStillRunsReleaseAdmission()
    {
        var provider = new RecordingImportProvider { LoadResult = 1 };
        provider.State.BootstrapCompleted = true;
        provider.State.LastCompletedUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var service = CreateService(
            new FakeIgdbClient([new IgdbGameType(7, "Main game")], [], _ => []),
            provider,
            new IgdbOptions { ImportEnabled = true, ClientId = "id", ClientSecret = "secret" });

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(0);
        result.RunCompleted.Should().BeTrue();
        result.EligibleGamesLoaded.Should().Be(1);
        provider.LoadCalls.Should().Be(1);
    }

    [Fact]
    public async Task BootstrapStopsOnBacklogBeforeFetchingUpstream()
    {
        var provider = new RecordingImportProvider();
        provider.BoundedAdmissionResults.Enqueue(new IgdbAdmissionResult(0, 0, 3, false, false));
        var client = new FakeIgdbClient([new IgdbGameType(7, "Main game")], [Game(10, DateTimeOffset.UtcNow)]);
        var service = CreateService(client, provider,
            new IgdbOptions { ImportEnabled = true, ClientId = "id", ClientSecret = "secret" });
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 10, TimeSpan.FromMinutes(1)));
        using var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 5, 10, 2, TimeSpan.FromMinutes(1)));

        var result = await service.ImportAsync(unit, bootstrap: true);

        result.StopReason.Should().Be(ImportStopReason.UnitAdmissionLimit);
        result.AdmissionRemaining.Should().Be(3);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task BootstrapRunsAdmissionAfterPageAndStopsBeforeNextFetch()
    {
        var provider = new RecordingImportProvider();
        provider.BoundedAdmissionResults.Enqueue(new IgdbAdmissionResult(0, 0, 0, true, false));
        provider.BoundedAdmissionResults.Enqueue(new IgdbAdmissionResult(0, 0, 1, false, false));
        var client = new FakeIgdbClient([new IgdbGameType(7, "Main game")], [Game(10, DateTimeOffset.UtcNow)]);
        var service = CreateService(client, provider,
            new IgdbOptions { ImportEnabled = true, ClientId = "id", ClientSecret = "secret", PageSize = 10 });
        using var session = new ImportBudgetSession(new ImportSessionLimits(10, 10, TimeSpan.FromMinutes(1)));
        using var unit = session.CreateUnit(new ImportWorkUnitLimits(5, 5, 10, 2, TimeSpan.FromMinutes(1)));

        var result = await service.ImportAsync(unit, bootstrap: true);

        result.StopReason.Should().Be(ImportStopReason.UnitAdmissionLimit);
        result.PagesCommitted.Should().Be(1);
        client.Queries.Should().ContainSingle();
        provider.Completed.Should().BeFalse();
    }

    [Fact]
    public async Task SessionAdmissionCapStopsBeforeDiscoveryEvenWhenBacklogWasDrained()
    {
        var provider = new RecordingImportProvider { LoadResult = 1, ReserveAdmission = true };
        var client = new FakeIgdbClient([], []);
        using var session = new ImportBudgetSession(new(10, 1, TimeSpan.FromMinutes(1)));
        using var unit = session.CreateUnit(new(5, 10, 1, 1, TimeSpan.FromMinutes(1)));
        var result = await CreateService(client, provider, new IgdbOptions { ImportEnabled = true })
            .ImportAsync(unit, bootstrap: true);
        result.StopReason.Should().Be(ImportStopReason.SessionAdmissionLimit);
        result.EligibleGamesLoaded.Should().Be(1);
        result.CatalogReady.Should().BeFalse();
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExhaustedUnitHttpAllowanceReportsHttpRatherThanPageLimit()
    {
        var provider = new RecordingImportProvider();
        var client = new FakeIgdbClient([], []);
        using var session = new ImportBudgetSession(new(10, 10, TimeSpan.FromMinutes(1)));
        using var unit = session.CreateUnit(new(5, 1, 10, 1, TimeSpan.FromMinutes(1)));
        unit.TryReserveHttp("game_types", out _).Should().BeTrue();
        var result = await CreateService(client, provider, new IgdbOptions { ImportEnabled = true })
            .ImportAsync(unit, bootstrap: true);
        result.StopReason.Should().Be(ImportStopReason.UnitHttpLimit);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task NonIncreasingPageStopsWithoutCommittingItsCursor()
    {
        var provider = new RecordingImportProvider();
        var client = new FakeIgdbClient([new(7, "Main game")],
            [Game(10, DateTimeOffset.UtcNow), Game(9, DateTimeOffset.UtcNow)]);
        var result = await CreateService(client, provider, new IgdbOptions { ImportEnabled = true }).ImportAsync();
        result.StopReason.Should().Be(ImportStopReason.NonIncreasingPage);
        provider.Committed.Should().BeEmpty();
        provider.LastCommittedId.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledInvocationsResumeBootstrapOrFixedIncrementalWindowWithinPageBound(bool incremental)
    {
        var provider = new RecordingImportProvider();
        provider.State.BootstrapCompleted = incremental;
        provider.State.LastCompletedUpdatedAt = DateTimeOffset.UtcNow.AddDays(-60);
        var client = new FakeIgdbClient([new(7, "Main game")], [],
            query => [Game(query.AfterId + 1, DateTimeOffset.UtcNow)]);
        var service = CreateService(client, provider, new IgdbOptions
        {
            ImportEnabled = true, PageBudget = 2, PageSize = 1
        });
        var first = await service.ImportAsync();
        var upper = provider.State.RunUpdatedBefore;
        var lower = provider.State.RunUpdatedAfter;
        var second = await service.ImportAsync();
        first.PagesCommitted.Should().Be(2);
        second.PagesCommitted.Should().Be(2);
        first.RunCompleted.Should().BeFalse();
        second.RunCompleted.Should().BeFalse();
        second.StopReason.Should().Be(ImportStopReason.UnitPageLimit);
        client.Queries.Select(x => x.AfterId).Should().Equal(0, 1, 2, 3);
        provider.State.LastCommittedId.Should().Be(4);
        provider.State.RunUpdatedBefore.Should().Be(upper);
        provider.State.RunUpdatedAfter.Should().Be(lower);
        if (incremental) upper.Should().NotBeNull();
        else provider.State.RunMaximumId.Should().Be(10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledInvocationAggregatesMultipleUnitsWithinTotalPageBudget(bool useFreshScopes)
    {
        var provider = new RecordingImportProvider { ReserveAdmission = true };
        var client = new FakeIgdbClient([new(7, "Main game")], [],
            query => [Game(query.AfterId + 1, DateTimeOffset.UtcNow)]);
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            PageBudget = 3,
            PageSize = 1,
            WorkUnitPageLimit = 1,
            WorkUnitAdmissionRowLimit = 10,
            WorkUnitAdmissionBatchLimit = 10,
            ScheduledSessionHttpAttemptLimit = 100,
            ScheduledSessionAdmissionRowLimit = 100
        };
        var scopes = useFreshScopes
            ? new RecordingScopeFactory(() => CreateService(client, provider, options))
            : null;
        var service = CreateService(client, provider, options, scopes);

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(3);
        result.RowsCommitted.Should().Be(3);
        result.EligibleGamesLoaded.Should().Be(3);
        result.AdmissionRows.Should().Be(3);
        result.AdmissionBatches.Should().Be(3);
        result.HttpAttempts.Should().Be(7);
        result.StopReason.Should().Be(ImportStopReason.UnitPageLimit);
        result.RunCompleted.Should().BeFalse();
        result.LeaseAcquired.Should().BeTrue();
        client.Queries.Select(x => x.AfterId).Should().Equal(0, 1, 2);
        provider.Completed.Should().BeFalse();
        if (scopes is not null)
        {
            scopes.CreatedScopes.Should().Be(3);
            scopes.DisposedScopes.Should().Be(3);
        }
    }

    [Fact]
    public async Task ScheduledInvocationStopsWhenCumulativeHttpLimitIsReached()
    {
        var provider = new RecordingImportProvider { ReserveAdmission = true };
        var client = new FakeIgdbClient([new(7, "Main game")], [],
            query => [Game(query.AfterId + 1, DateTimeOffset.UtcNow)]);
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            PageBudget = 5,
            PageSize = 1,
            WorkUnitPageLimit = 1,
            WorkUnitAdmissionRowLimit = 10,
            WorkUnitAdmissionBatchLimit = 10,
            WorkUnitHttpAttemptLimit = 20,
            ScheduledSessionHttpAttemptLimit = 4,
            ScheduledSessionAdmissionRowLimit = 100
        };
        var scopes = new RecordingScopeFactory(() => CreateService(client, provider, options));
        var service = CreateService(client, provider, options, scopes);

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(1);
        result.HttpAttempts.Should().Be(4);
        result.StopReason.Should().Be(ImportStopReason.SessionHttpLimit);
        result.RunCompleted.Should().BeFalse();
        client.Queries.Should().ContainSingle();
        scopes.CreatedScopes.Should().Be(2);
        scopes.DisposedScopes.Should().Be(2);
    }

    [Fact]
    public async Task ScheduledInvocationCarriesAdmissionRowsAcrossUnitsAndStopsAtSessionLimit()
    {
        var provider = new RecordingImportProvider { ReserveAdmission = true };
        var client = new FakeIgdbClient([new(7, "Main game")], [],
            query => [Game(query.AfterId + 1, DateTimeOffset.UtcNow)]);
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            PageBudget = 5,
            PageSize = 1,
            WorkUnitPageLimit = 1,
            WorkUnitHttpAttemptLimit = 20,
            WorkUnitAdmissionRowLimit = 1,
            WorkUnitAdmissionBatchLimit = 1,
            ScheduledSessionHttpAttemptLimit = 100,
            ScheduledSessionAdmissionRowLimit = 2
        };
        var scopes = new RecordingScopeFactory(() => CreateService(client, provider, options));
        var service = CreateService(client, provider, options, scopes);

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(2);
        result.EligibleGamesLoaded.Should().Be(2);
        result.AdmissionRows.Should().Be(2);
        result.AdmissionBatches.Should().Be(2);
        result.StopReason.Should().Be(ImportStopReason.SessionAdmissionLimit);
        result.RunCompleted.Should().BeFalse();
        client.Queries.Select(x => x.AfterId).Should().Equal(0, 1);
        scopes.CreatedScopes.Should().Be(2);
        scopes.DisposedScopes.Should().Be(2);
    }

    [Fact]
    public async Task ScheduledInvocationDoesNotStartIncrementalWindowAfterCurrentWindowCompletes()
    {
        var provider = new RecordingImportProvider();
        provider.State.BootstrapCompleted = true;
        provider.State.LastCompletedUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var client = new FakeIgdbClient([new(7, "Main game")], [],
            query => [Game(query.AfterId + 1, DateTimeOffset.UtcNow)]);
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            PageBudget = 5,
            PageSize = 2,
            WorkUnitPageLimit = 1
        };
        var scopes = new RecordingScopeFactory(() => CreateService(client, provider, options));
        var service = CreateService(client, provider, options, scopes);

        var result = await service.ImportAsync();

        result.PagesCommitted.Should().Be(1);
        result.RunCompleted.Should().BeTrue();
        result.StopReason.Should().Be(ImportStopReason.Completed);
        provider.Completed.Should().BeTrue();
        provider.StartRunCalls.Should().Be(1);
        scopes.CreatedScopes.Should().Be(1);
        client.Queries.Should().ContainSingle();
    }

    [Fact]
    public async Task ScheduledInvocationStopsOnUnitLimitWithoutDurableOrAdmissionProgress()
    {
        var provider = new RecordingImportProvider();
        var client = new FakeIgdbClient([new(7, "Main game")], [Game(10, DateTimeOffset.UtcNow)]);
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            PageBudget = 5,
            PageSize = 1,
            WorkUnitPageLimit = 1,
            WorkUnitHttpAttemptLimit = 2,
            ScheduledSessionHttpAttemptLimit = 100
        };
        var scopes = new RecordingScopeFactory(() => CreateService(client, provider, options));
        var service = CreateService(client, provider, options, scopes);

        var result = await service.ImportAsync();

        result.StopReason.Should().Be(ImportStopReason.UnitHttpLimit);
        result.HttpAttempts.Should().Be(2);
        result.PagesCommitted.Should().Be(0);
        client.Queries.Should().BeEmpty();
        scopes.CreatedScopes.Should().Be(1);
    }

    [Fact]
    public async Task ScheduledInvocationRetainsCommittedCountsWhenCanceledBetweenUnits()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingImportProvider { ReserveAdmission = true };
        provider.AfterPageCommit = cancellation.Cancel;
        var client = new FakeIgdbClient([new(7, "Main game")], [],
            query => [Game(query.AfterId + 1, DateTimeOffset.UtcNow)]);
        var options = new IgdbOptions
        {
            ImportEnabled = true,
            PageBudget = 5,
            PageSize = 1,
            WorkUnitPageLimit = 1,
            WorkUnitAdmissionRowLimit = 10,
            WorkUnitAdmissionBatchLimit = 10
        };
        var scopes = new RecordingScopeFactory(() => CreateService(client, provider, options));
        var service = CreateService(client, provider, options, scopes);

        var result = await service.ImportAsync(cancellation.Token);

        result.PagesCommitted.Should().Be(1);
        result.RowsCommitted.Should().Be(1);
        result.EligibleGamesLoaded.Should().Be(1);
        result.StopReason.Should().Be(ImportStopReason.Canceled);
        result.RunCompleted.Should().BeFalse();
        scopes.CreatedScopes.Should().Be(1);
    }

    private static IgdbImportService CreateService(IIgdbClient client, RecordingImportProvider provider, IgdbOptions options,
        IServiceScopeFactory? scopeFactory = null) =>
        new(client, provider, Options.Create(options), Options.Create(new ArtworkOptions()),
            NullLogger<IgdbImportService>.Instance, scopeFactory);

    private static IgdbGame Game(long id, DateTimeOffset updatedAt) =>
        new(id, $"Game {id}", DateTimeOffset.UtcNow.AddDays(-1), 7, null, id % 2 == 0 ? null : $"cover-{id}", updatedAt);

    private sealed class FakeIgdbClient(
        IReadOnlyList<IgdbGameType> types,
        IReadOnlyList<IgdbGame> games,
        Func<IgdbGameQuery, IReadOnlyList<IgdbGame>>? pageFactory = null) : IIgdbClient
    {
        private readonly Func<IgdbGameQuery, IReadOnlyList<IgdbGame>> pageProvider = pageFactory ?? (query => query.AfterId == 0 ? games : (IReadOnlyList<IgdbGame>)[]);
        public int Calls { get; private set; }
        public List<IgdbGameQuery> Queries { get; } = [];
        public Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> GetMaximumGameIdAsync(CancellationToken ct) { Calls++; return Task.FromResult(10L); }
        public Task<long> GetMaximumGameIdAsync(ImportWorkUnitBudget budget, CancellationToken ct)
        {
            if (!budget.TryReserveHttp("maximum_id", out var reason)) throw new ImportBudgetExceededException(reason);
            return GetMaximumGameIdAsync(ct);
        }
        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct) { Calls++; return Task.FromResult(types); }
        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(ImportWorkUnitBudget budget, CancellationToken ct)
        {
            if (!budget.TryReserveHttp("game_types", out var reason)) throw new ImportBudgetExceededException(reason);
            return GetGameTypesAsync(ct);
        }
        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct)
        {
            Calls++;
            Queries.Add(query);
            return Task.FromResult(pageProvider(query));
        }
        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, ImportWorkUnitBudget budget, CancellationToken ct)
        {
            if (!budget.TryReservePage(out var reason)) throw new ImportBudgetExceededException(reason);
            if (!budget.TryReserveHttp("games", out reason)) throw new ImportBudgetExceededException(reason);
            return GetGamesAsync(query, ct);
        }
    }

    private sealed class RecordingImportProvider : IIgdbImportProvider
    {
        private IgdbImportState state = new();
        public bool ReturnLease { get; set; } = true;
        public List<IgdbImport> Committed { get; } = [];
        public long LastCommittedId { get; private set; }
        public bool Completed { get; private set; }
        public int StartRunCalls { get; private set; }
        public int LoadCalls { get; private set; }
        public int LoadResult { get; set; }
        public bool ReserveAdmission { get; set; }
        public Action? AfterPageCommit { get; set; }
        public IgdbImportState State => state;
        private int admitted;
        public Queue<IgdbAdmissionResult> BoundedAdmissionResults { get; } = [];

        public Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct) =>
            Task.FromResult<IgdbImportLease?>(ReturnLease ? new IgdbImportLease(state, Guid.NewGuid(), leaseDuration) : null);
        public Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? maximumId, DateTimeOffset? updatedAfter, DateTimeOffset? updatedBefore, DateTimeOffset now, CancellationToken ct)
        {
            StartRunCalls++;
            state.RunIsBootstrap = bootstrap; state.RunMaximumId = maximumId; state.RunUpdatedAfter = updatedAfter; state.RunUpdatedBefore = updatedBefore; lease.State = state;
            return Task.FromResult(state);
        }
        public Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long committedId, CancellationToken ct)
        {
            Committed.AddRange(page); LastCommittedId = committedId; state.LastCommittedId = committedId; AfterPageCommit?.Invoke(); return Task.FromResult(state);
        }
        public Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct) { Completed = true; return Task.CompletedTask; }
        public Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct) => Task.CompletedTask;
        public Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
        {
            LoadCalls++;
            return Task.FromResult(LoadEligibleCount(now, supportedGameTypes));
        }

        public Task<IgdbAdmissionResult> LoadEligibleGamesAsync(IgdbImportLease lease,
            IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration,
            TimeSpan negativeCacheDuration, ImportWorkUnitBudget budget, CancellationToken ct)
        {
            LoadCalls++;
            if (BoundedAdmissionResults.TryDequeue(out var result))
                return Task.FromResult(result);
            var pending = GetPendingEligibleCount(now, supportedGameTypes);
            if (ReserveAdmission && pending > 0)
            {
                if (!budget.TryReserveAdmission(pending, out var rowReason))
                    return Task.FromResult(new IgdbAdmissionResult(0, 0, pending, false, false, rowReason.ToString()));
                if (!budget.TryReserveAdmissionBatch(out var batchReason))
                    return Task.FromResult(new IgdbAdmissionResult(0, 0, pending, false, false, batchReason.ToString()));
            }
            admitted += pending;
            return Task.FromResult(new IgdbAdmissionResult(pending, pending == 0 ? 0 : 1, 0, true, false));
        }

        private int LoadEligibleCount(DateTimeOffset now, IReadOnlySet<string> supportedGameTypes)
        {
            var pending = GetPendingEligibleCount(now, supportedGameTypes);
            admitted += pending;
            return pending;
        }

        private int GetPendingEligibleCount(DateTimeOffset now, IReadOnlySet<string> supportedGameTypes)
        {
            if (LoadResult != 0)
                return Math.Max(0, LoadResult - admitted);
            var count = Committed.Count(x => x.Name is not null && x.FirstReleaseDate <= now && x.VersionParentId is null && x.GameTypeName is not null && supportedGameTypes.Contains(x.GameTypeName));
            return Math.Max(0, count - admitted);
        }
    }

    private sealed class RecordingScopeFactory(Func<IgdbImportService> createService) : IServiceScopeFactory
    {
        public int CreatedScopes { get; private set; }
        public int DisposedScopes { get; private set; }

        public IServiceScope CreateScope()
        {
            CreatedScopes++;
            return new RecordingScope(createService(), () => DisposedScopes++);
        }

        private sealed class RecordingScope(IgdbImportService service, Action onDispose) : IServiceScope, IAsyncDisposable
        {
            private bool disposed;
            public IServiceProvider ServiceProvider { get; } = new RecordingServiceProvider(service);

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                onDispose();
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }

        private sealed class RecordingServiceProvider(IgdbImportService service) : IServiceProvider
        {
            public object? GetService(Type serviceType) => serviceType == typeof(IgdbImportService) ? service : null;
        }
    }
}
