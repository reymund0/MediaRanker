using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbLoadServiceTests
{
    [Fact]
    public async Task LoadRunsDependencyOrderWithBoundedUnitsAndReturnsAffectedCounts()
    {
        var provider = new FakeLoadProvider();
        var options = Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 2 });
        var service = new ImdbLoadService(provider, options, NullLogger<ImdbLoadService>.Instance);

        var result = await service.LoadAsync();

        result.Affected.Should().Be(8);
        provider.Stages.Should().Equal("non-series", "non-series", "series", "series", "seasons", "seasons", "episodes", "episodes");
        provider.RequestedCaps.Should().OnlyContain(cap => cap == 2);
    }

    [Fact]
    public async Task CancellationStopsBetweenUnits()
    {
        var provider = new FakeLoadProvider { CancelAfterFirstBatch = true };
        var service = new ImdbLoadService(provider,
            Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 2 }), NullLogger<ImdbLoadService>.Instance);
        var cancellation = new CancellationTokenSource();

        var run = async () => await service.LoadNonSeriesMediaAsync(cancellation.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FullReplayUsesTheSameBoundedSequence()
    {
        var firstProvider = new FakeLoadProvider();
        var service = new ImdbLoadService(firstProvider,
            Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 2 }), NullLogger<ImdbLoadService>.Instance);

        var first = await service.LoadNonSeriesMediaAsync();
        var secondProvider = new FakeLoadProvider();
        var secondService = new ImdbLoadService(secondProvider,
            Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 2 }), NullLogger<ImdbLoadService>.Instance);
        var second = await secondService.LoadNonSeriesMediaAsync();

        first.Affected.Should().Be(second.Affected);
        firstProvider.RequestedCaps.Concat(secondProvider.RequestedCaps).Should().OnlyContain(cap => cap == 2);
    }

    [Fact]
    public async Task EpisodeReplayAdvancesPastExistingRowToLaterKeys()
    {
        var provider = new FakeLoadProvider { EpisodeConflictThenNew = true };
        var service = new ImdbLoadService(provider,
            Options.Create(new ImdbImportOptions { MaxLoadRowsPerUnit = 2 }), NullLogger<ImdbLoadService>.Instance);

        var result = await service.LoadEpisodeMediaAsync();

        result.Affected.Should().Be(2);
        provider.EpisodeCalls.Should().Be(2);
        provider.EpisodeCursors.Should().Equal(null, "tt-existing");
    }

    private sealed class FakeLoadProvider : IImdbLoadProvider
    {
        public bool CancelAfterFirstBatch { get; init; }
        public bool EpisodeConflictThenNew { get; init; }
        public int EpisodeCalls { get; private set; }
        public List<string?> EpisodeCursors { get; } = [];
        public List<string> Stages { get; } = [];
        public List<int> RequestedCaps { get; } = [];
        private readonly Dictionary<string, int> calls = [];

        public Task<ImdbLoadBatchResult> LoadNonSeriesMediaBatchAsync(int minVotesMovies, string? after, int maxRows, CancellationToken ct) => Batch("non-series", 1, after, maxRows, ct);
        public Task<ImdbLoadBatchResult> LoadSeriesCollectionsBatchAsync(int minVotesTv, string? after, int maxRows, CancellationToken ct) => Batch("series", 1, after, maxRows, ct);
        public Task<ImdbSeasonLoadBatchResult> LoadSeasonCollectionsBatchAsync(string? parent, int? season, int maxGroups, CancellationToken ct)
        {
            Stages.Add("seasons");
            RequestedCaps.Add(maxGroups);
            var more = NextCall("seasons") == 1;
            return Task.FromResult(new ImdbSeasonLoadBatchResult(1, "tt0000001", more ? 1 : 2, more));
        }
        public Task<ImdbLoadBatchResult> LoadEpisodeMediaBatchAsync(string? after, int maxRows, CancellationToken ct) => Batch("episodes", 1, after, maxRows, ct);

        private Task<ImdbLoadBatchResult> Batch(string stage, int affected, string? after, int cap, CancellationToken ct)
        {
            Stages.Add(stage);
            RequestedCaps.Add(cap);
            if (stage == "episodes" && EpisodeConflictThenNew)
            {
                EpisodeCursors.Add(after);
                EpisodeCalls++;
                return Task.FromResult(EpisodeCalls == 1
                    ? new ImdbLoadBatchResult(1, "tt-existing", true)
                    : new ImdbLoadBatchResult(1, null, false));
            }
            var hasMore = NextCall(stage) == 1;
            if (CancelAfterFirstBatch) throw new OperationCanceledException(ct);
            return Task.FromResult(new ImdbLoadBatchResult(affected, hasMore ? "next" : null, hasMore));
        }

        private int NextCall(string stage) => calls.TryGetValue(stage, out var count) ? calls[stage] = count + 1 : calls[stage] = 1;

        public Task<ImdbLoadResult> LoadNonSeriesMediaAsync(int minVotesMovies, CancellationToken ct) => throw new NotSupportedException();
        public Task<ImdbLoadResult> LoadSeriesCollectionsAsync(int minVotesTv, CancellationToken ct) => throw new NotSupportedException();
        public Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
