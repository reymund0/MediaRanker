using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbImportServiceTests
{
    [Fact]
    public async Task CompleteFeedsRunCleanupOnlyAfterAllFeedBatchesAndCarryCounters()
    {
        var provider = new FakeImportProvider();
        var service = CreateService(provider, new Dictionary<string, string>
        {
            ["ratings"] = "tconst\taverageRating\tnumVotes\ntt0000001\t8.0\t10\n",
            ["basics"] = "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres\ntt0000001\tmovie\tTitle\tTitle\t0\t2020\t\\N\t90\tDrama\n",
            ["episodes"] = "tconst\tparentTconst\tseasonNumber\tepisodeNumber\ntt0000002\ttt0000003\t\\N\t1\n"
        }, batchSize: 1);

        var result = await service.ImportAsync();

        result.Completed.Should().BeTrue();
        result.Counters!.RowsRead.Should().Be(3);
        provider.CleanupCalls.Should().Be(4);
        provider.BasicsRows.Single().StartYear.Should().Be(2020);
        provider.EpisodeRows.Single().SeasonNumber.Should().Be(-1);
    }

    [Fact]
    public async Task FailedBasicsBatchPropagatesAndDoesNotCleanup()
    {
        var provider = new FakeImportProvider { FailBasics = true };
        var service = CreateService(provider, CompleteFeeds(), batchSize: 1);

        var run = () => service.ImportAsync();

        await run.Should().ThrowAsync<InvalidOperationException>();
        provider.CleanupCalls.Should().Be(0);
        provider.EpisodeCalls.Should().Be(0);
    }

    [Fact]
    public async Task FailedEpisodeBatchPropagatesAndDoesNotCleanup()
    {
        var provider = new FakeImportProvider { FailEpisodes = true };
        var service = CreateService(provider, CompleteFeeds(), batchSize: 1);

        var run = () => service.ImportAsync();

        await run.Should().ThrowAsync<InvalidOperationException>();
        provider.CleanupCalls.Should().Be(0);
    }

    [Fact]
    public async Task StrictRequiredValueErrorPreventsCleanup()
    {
        var provider = new FakeImportProvider();
        var feeds = CompleteFeeds();
        feeds["ratings"] = feeds["ratings"].Replace("8.0", "11.0", StringComparison.Ordinal);
        var service = CreateService(provider, feeds, batchSize: 1);

        var run = () => service.ImportAsync();

        await run.Should().ThrowAsync<InvalidDataException>();
        provider.CleanupCalls.Should().Be(0);
    }

    [Fact]
    public async Task RepeatedInvocationStartsCountersFresh()
    {
        var provider = new FakeImportProvider();
        var service = CreateService(provider, CompleteFeeds(), batchSize: 2);

        var first = await service.ImportAsync();
        var second = await service.ImportAsync();

        second.Counters!.RowsRead.Should().Be(first.Counters!.RowsRead);
        second.Counters.BatchesCommitted.Should().Be(first.Counters.BatchesCommitted);
        provider.CutoffCalls.Should().Be(2);
        provider.Cutoffs.Should().HaveCount(2);
        provider.Cutoffs[0].Should().NotBe(provider.Cutoffs[1]);
    }

    [Fact]
    public async Task CleanupUsesOneDatabaseCutoffAcrossBoundedUnits()
    {
        var provider = new FakeImportProvider { StaleDeleteResults = new Queue<int>([2, 1, 0]) };
        var service = CreateService(provider, CompleteFeeds(), batchSize: 1);

        await service.ImportAsync();

        provider.StaleCutoffs.Should().HaveCount(3);
        provider.StaleCutoffs.Should().OnlyContain(cutoff => cutoff == provider.StaleCutoffs[0]);
    }

    [Fact]
    public async Task CleanupFailurePropagatesAfterFeedsAndPreventsCompletedOutcome()
    {
        var provider = new FakeImportProvider { FailCleanup = true };
        var service = CreateService(provider, CompleteFeeds(), batchSize: 1);

        var run = () => service.ImportAsync();

        await run.Should().ThrowAsync<InvalidOperationException>();
        provider.BasicsRows.Should().NotBeEmpty();
        provider.CleanupCalls.Should().Be(1);
    }

    [Fact]
    public async Task OptionalNullsAndIntentionalFiltersRemainValid()
    {
        var provider = new FakeImportProvider();
        var feeds = CompleteFeeds();
        feeds["basics"] = "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres\n"
            + "tt0000001\tmovie\tTitle\tTitle\t0\t\\N\t\\N\t\\N\t\\N\n"
            + "tt0000004\tmovie\tAdult\tAdult\t1\t2020\t\\N\t90\t\\N\n"
            + "tt0000005\tvideoGame\tGame\tGame\t0\t2020\t\\N\t90\t\\N\n";
        var service = CreateService(provider, feeds, batchSize: 1);

        var result = await service.ImportAsync();

        result.Completed.Should().BeTrue();
        provider.BasicsRows.Should().ContainSingle(row => row.Tconst == "tt0000001");
        provider.BasicsRows[0].StartYear.Should().BeNull();
    }

    private static ImdbImportService CreateService(FakeImportProvider provider, Dictionary<string, string> feeds, int batchSize)
    {
        var options = new ImdbImportOptions
        {
            DatasetUrl = "https://fixture.invalid/basics",
            EpisodesDatasetUrl = "https://fixture.invalid/episodes",
            RatingsDatasetUrl = "https://fixture.invalid/ratings",
            BatchSize = batchSize,
            MaxRowsPerFeed = 10_000
        };
        var handler = new FeedHandler(feeds);
        var parser = new ImdbTsvProvider(new HttpClient(handler), Options.Create(options), NullLogger<ImdbTsvProvider>.Instance);
        return new ImdbImportService(parser, provider, Options.Create(options), NullLogger<ImdbImportService>.Instance);
    }

    private static Dictionary<string, string> CompleteFeeds() => new()
    {
        ["ratings"] = "tconst\taverageRating\tnumVotes\ntt0000001\t8.0\t10\n",
        ["basics"] = "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres\ntt0000001\tmovie\tTitle\tTitle\t0\t2020\t\\N\t90\tDrama\n",
        ["episodes"] = "tconst\tparentTconst\tseasonNumber\tepisodeNumber\ntt0000002\ttt0000003\t\\N\t1\n"
    };

    private sealed class FakeImportProvider : IImdbImportProvider
    {
        public bool FailBasics { get; init; }
        public bool FailEpisodes { get; init; }
        public bool FailCleanup { get; init; }
        public int CleanupCalls { get; private set; }
        public int CutoffCalls { get; private set; }
        public int EpisodeCalls { get; private set; }
        public List<ImdbTsvRow> BasicsRows { get; } = [];
        public List<ImdbEpisodeTsvRow> EpisodeRows { get; } = [];
        public List<DateTimeOffset> Cutoffs { get; } = [];
        public List<DateTimeOffset> StaleCutoffs { get; } = [];
        public Queue<int> StaleDeleteResults { get; init; } = new([0]);

        public Task<ImdbImportResult> ImportRatingsAsync(List<ImdbRatingTsvRow> rows, CancellationToken ct) => Task.FromResult(new ImdbImportResult(rows.Count, 0));
        public Task<ImdbImportResult> ImportBasicsAsync(List<ImdbTsvRow> rows, CancellationToken ct)
        {
            if (FailBasics) throw new InvalidOperationException("fixture batch failure");
            BasicsRows.AddRange(rows);
            return Task.FromResult(new ImdbImportResult(rows.Count, 0));
        }
        public Task<ImdbImportResult> ImportEpisodesAsync(List<ImdbEpisodeTsvRow> rows, CancellationToken ct)
        {
            EpisodeCalls++;
            if (FailEpisodes) throw new InvalidOperationException("fixture batch failure");
            EpisodeRows.AddRange(rows);
            return Task.FromResult(new ImdbImportResult(rows.Count, 0));
        }
        public Task<DateTimeOffset> GetDatabaseUtcNowAsync(CancellationToken ct)
        {
            CutoffCalls++;
            var cutoff = DateTimeOffset.UnixEpoch.AddSeconds(CutoffCalls);
            Cutoffs.Add(cutoff);
            return Task.FromResult(cutoff);
        }
        public Task<int> DeleteFutureImportsAsync(int maxRows, CancellationToken ct) => Cleanup();
        public Task<int> DeleteTvPilotImportsAsync(int maxRows, CancellationToken ct) => Cleanup();
        public Task<int> DeleteOrphanEpisodesAsync(int maxRows, CancellationToken ct) => Cleanup();
        public Task<int> DeleteStaleRatingsAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct)
        {
            CleanupCalls++;
            if (FailCleanup) throw new InvalidOperationException("fixture cleanup failure");
            StaleCutoffs.Add(cutoffUtc);
            return Task.FromResult(StaleDeleteResults.Count == 0 ? 0 : StaleDeleteResults.Dequeue());
        }
        public Task<int> DeleteFutureImportsAsync(CancellationToken ct) => Cleanup();
        public Task<int> DeleteTvPilotImportsAsync(CancellationToken ct) => Cleanup();
        public Task<int> DeleteOrphanEpisodesAsync(CancellationToken ct) => Cleanup();
        public Task<int> DeleteStaleRatingsAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => Cleanup();
        private Task<int> Cleanup()
        {
            CleanupCalls++;
            if (FailCleanup) throw new InvalidOperationException("fixture cleanup failure");
            return Task.FromResult(0);
        }
    }

    private sealed class FeedHandler(Dictionary<string, string> feeds) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var key = request.RequestUri!.AbsolutePath.Trim('/');
            var content = Compress(feeds[key]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }

    private static byte[] Compress(string content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(content));
        return output.ToArray();
    }
}
