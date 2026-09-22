using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
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

        result.Should().Be(new IgdbImportRunResult(1, 1, 1, true, true));
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

        result.Should().Be(new IgdbImportRunResult(2, 4, 4, true, true));
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

    private static IgdbImportService CreateService(IIgdbClient client, RecordingImportProvider provider, IgdbOptions options) =>
        new(client, provider, Options.Create(options), Options.Create(new ArtworkOptions()), NullLogger<IgdbImportService>.Instance);

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
        public Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct) { Calls++; return Task.FromResult(types); }
        public Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct)
        {
            Calls++;
            Queries.Add(query);
            return Task.FromResult(pageProvider(query));
        }
    }

    private sealed class RecordingImportProvider : IIgdbImportProvider
    {
        private IgdbImportState state = new();
        public bool ReturnLease { get; set; } = true;
        public List<IgdbImport> Committed { get; } = [];
        public long LastCommittedId { get; private set; }
        public bool Completed { get; private set; }
        public int LoadCalls { get; private set; }
        public int LoadResult { get; set; }
        public IgdbImportState State => state;

        public Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct) =>
            Task.FromResult<IgdbImportLease?>(ReturnLease ? new IgdbImportLease(state, Guid.NewGuid(), leaseDuration) : null);
        public Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? maximumId, DateTimeOffset? updatedAfter, DateTimeOffset? updatedBefore, DateTimeOffset now, CancellationToken ct)
        {
            state.RunIsBootstrap = bootstrap; state.RunMaximumId = maximumId; state.RunUpdatedAfter = updatedAfter; state.RunUpdatedBefore = updatedBefore; lease.State = state;
            return Task.FromResult(state);
        }
        public Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long committedId, CancellationToken ct)
        {
            Committed.AddRange(page); LastCommittedId = committedId; state.LastCommittedId = committedId; return Task.FromResult(state);
        }
        public Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct) { Completed = true; return Task.CompletedTask; }
        public Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct) => Task.CompletedTask;
        public Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
        {
            LoadCalls++;
            if (LoadResult != 0) return Task.FromResult(LoadResult);
            var count = Committed.Count(x => x.Name is not null && x.FirstReleaseDate <= now && x.VersionParentId is null && x.GameTypeName is not null && supportedGameTypes.Contains(x.GameTypeName));
            return Task.FromResult(count);
        }
    }
}
