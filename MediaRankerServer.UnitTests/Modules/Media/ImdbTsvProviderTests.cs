using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Text;
using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbTsvProviderTests
{
    private readonly ITestOutputHelper output;

    public ImdbTsvProviderTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task CompleteGzipImportsEveryBatch()
    {
        var provider = Create(Compress("id\tname\n1\tone\n2\ttwo\n3\tthree\n"));
        var imported = new List<string>();

        var result = await provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (batch, _) =>
            {
                imported.AddRange(batch);
                return Task.CompletedTask;
            });

        imported.Should().Equal("1", "2", "3");
        result.RowsRead.Should().Be(3);
        result.Batches.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task MissingGzipTrailerDoesNotReportCompleteIngestion(int removedBytes)
    {
        var bytes = Compress("id\tname\n1\tone\n2\ttwo\n");
        var provider = Create(bytes[..^removedBytes]);

        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        await run.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task TrailingGarbageDoesNotReportCompleteIngestion()
    {
        var bytes = Compress("id\tname\n1\tone\n")
            .Concat(Encoding.UTF8.GetBytes("trailing"))
            .ToArray();
        await RunInvalidEnvelopeAsync(bytes);
    }

    [Fact]
    public async Task DuplicateValidTrailerDoesNotReportCompleteIngestion()
    {
        var bytes = Compress("id\tname\n1\tone\n");
        await RunInvalidEnvelopeAsync(bytes.Concat(bytes[^8..]).ToArray());
    }

    [Fact]
    public async Task ConcatenatedGzipMembersDoNotReportCompleteIngestion()
    {
        var bytes = Compress("id\tname\n1\tone\n").Concat(Compress("2\ttwo\n")).ToArray();
        await RunInvalidEnvelopeAsync(bytes);
    }

    [Fact]
    public async Task AdvertisedLengthMismatchDoesNotReportCompleteIngestion()
    {
        var bytes = Compress("id\tname\n1\tone\n");
        var provider = Create(new FixtureHandler(bytes, bytes.Length + 1));
        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);
        await run.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task StructuralRowErrorDoesNotReportCompleteIngestion()
    {
        var provider = Create(Compress("id\tname\n1\tone\nmalformed\n2\ttwo\n"));
        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);
        await run.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task CallbackFailurePropagatesWithoutSendingAnotherFeedRequest()
    {
        var handler = new FixtureHandler(Compress("id\tname\n1\tone\n2\ttwo\n3\tthree\n"));
        var provider = Create(handler);
        var run = () => provider.RunBatchImportAsync<string>("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => throw new InvalidOperationException("fixture batch failure"));
        await run.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task MaxLineCharactersBoundsLineAccumulation()
    {
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(Compress("id\tname\n1\t123456\n"))),
            Options.Create(new ImdbImportOptions { BatchSize = 2, MaxLineCharacters = 4 }), NullLogger<ImdbTsvProvider>.Instance);
        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);
        await run.Should().ThrowAsync<ImdbBudgetExceededException>();
    }

    [Fact]
    public async Task CompressedByteBudgetStopsBeforeTheFixtureIsParsed()
    {
        var bytes = Compress("id\tname\n1\tone\n");
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(bytes)),
            Options.Create(new ImdbImportOptions { MaxCompressedBytesPerFeed = bytes.Length - 1 }),
            NullLogger<ImdbTsvProvider>.Instance);

        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        await run.Should().ThrowAsync<ImdbBudgetExceededException>();
    }

    [Fact]
    public async Task TemporaryDiskBudgetStopsBeforeTheFixtureIsParsed()
    {
        var bytes = Compress("id\tname\n1\tone\n");
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(bytes)),
            Options.Create(new ImdbImportOptions { MaxTemporaryDiskBytes = bytes.Length - 1 }),
            NullLogger<ImdbTsvProvider>.Instance);

        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        await run.Should().ThrowAsync<ImdbBudgetExceededException>();
    }

    [Fact]
    public async Task DecompressedByteBudgetStopsBeforeAWholeLineIsAllocated()
    {
        var bytes = Compress($"id\tname\n1\t{new string('x', 1024)}\n");
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(bytes)),
            Options.Create(new ImdbImportOptions { MaxDecompressedBytesPerFeed = 64 }),
            NullLogger<ImdbTsvProvider>.Instance);

        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        await run.Should().ThrowAsync<ImdbBudgetExceededException>();
    }

    [Fact]
    public async Task RowBudgetStopsAtTheFiniteBoundary()
    {
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(Compress("id\tname\n1\tone\n2\ttwo\n"))),
            Options.Create(new ImdbImportOptions { BatchSize = 2, MaxRowsPerFeed = 1 }),
            NullLogger<ImdbTsvProvider>.Instance);
        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        await run.Should().ThrowAsync<ImdbBudgetExceededException>();
    }

    [Fact]
    public async Task BatchSizeAboveTheFiniteLimitIsRejectedBeforeSendingARequest()
    {
        var handler = new FixtureHandler(Compress("id\tname\n1\tone\n"));
        var provider = new ImdbTsvProvider(new HttpClient(handler),
            Options.Create(new ImdbImportOptions { BatchSize = ImdbImportOptions.MaximumBatchSize + 1 }),
            NullLogger<ImdbTsvProvider>.Instance);

        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        await run.Should().ThrowAsync<OptionsValidationException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ImportAttemptAllowanceStopsBeforeAnAdditionalHttpSend()
    {
        var handler = new FixtureHandler(Compress("id\tname\n1\tone\n"));
        var provider = Create(handler);
        var execution = new ImdbImportExecution(new ImdbImportOptions { MaxHttpAttempts = 3 });

        for (var attempt = 0; attempt < 3; attempt++)
            await provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
                (columns, _, _) => columns[0], (_, _) => Task.CompletedTask, execution);

        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask, execution);

        await run.Should().ThrowAsync<ImdbBudgetExceededException>();
        handler.Calls.Should().Be(3);
        execution.HttpAttempts.Should().Be(3);
    }

    [Fact]
    public async Task AggregateBatchCharacterLimitFlushesWideRowsBelowTheRowCapAndPreservesOrder()
    {
        var options = new ImdbImportOptions { BatchSize = 100, MaxLineCharacters = 12, MaxBatchCharacters = 14 };
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(Compress("id\tname\n1\talpha\n2\tbravo!\n3\tcharlie\n4\tdelta\n"))),
            Options.Create(options), NullLogger<ImdbTsvProvider>.Instance);
        var execution = new ImdbImportExecution(options);
        var batches = new List<List<string>>();

        var result = await provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (batch, _) =>
            {
                batches.Add(batch.ToList());
                return Task.CompletedTask;
            }, execution);

        batches.SelectMany(batch => batch).Should().Equal("1", "2", "3", "4");
        batches.Should().HaveCount(4).And.OnlyContain(batch => batch.Count == 1);
        result.Batches.Should().Be(4);
        execution.Counters.RowsRead.Should().Be(4);
        execution.Counters.ParsedRows.Should().Be(4);
        execution.Counters.FilteredRows.Should().Be(0);
        execution.Counters.BatchesCommitted.Should().Be(4);
    }

    [Fact]
    public async Task FilteredWideRowDoesNotFlushAnOtherwiseFittingBatch()
    {
        var options = new ImdbImportOptions { BatchSize = 100, MaxLineCharacters = 20, MaxBatchCharacters = 20 };
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(Compress("id\tname\n1\tabc\nskip\t12345678901\n2\tdefgh\n"))),
            Options.Create(options), NullLogger<ImdbTsvProvider>.Instance);
        var batches = new List<List<string>>();

        var result = await provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0] == "skip" ? null : columns[0], (batch, _) =>
            {
                batches.Add(batch.ToList());
                return Task.CompletedTask;
            });

        batches.Should().ContainSingle().Which.Should().Equal("1", "2");
        result.RowsRead.Should().Be(3);
        result.ParsedRows.Should().Be(2);
        result.FilteredRows.Should().Be(1);
        result.Batches.Should().Be(1);
    }

    [Fact]
    public async Task FailureDuringSizeTriggeredFlushPropagatesWithoutStartingAnotherBatch()
    {
        var options = new ImdbImportOptions { BatchSize = 100, MaxLineCharacters = 12, MaxBatchCharacters = 14 };
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(Compress("id\tname\n1\talpha\n2\tbravo!\n3\tcharlie\n"))),
            Options.Create(options), NullLogger<ImdbTsvProvider>.Instance);
        var execution = new ImdbImportExecution(options);
        var callbacks = 0;
        var rowsSent = new List<string>();

        Func<Task> run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (batch, _) =>
            {
                callbacks++;
                rowsSent.AddRange(batch);
                throw new InvalidOperationException("fixture batch failure");
            }, execution);

        await run.Should().ThrowAsync<InvalidOperationException>();
        callbacks.Should().Be(1);
        rowsSent.Should().Equal("1");
        execution.Counters.RowsRead.Should().Be(2);
        execution.Counters.ParsedRows.Should().Be(2);
        execution.Counters.BatchesCommitted.Should().Be(0);
    }

    [Fact]
    public async Task CancellationFromACommittedBatchStopsBeforeTheNextBatch()
    {
        var provider = new ImdbTsvProvider(new HttpClient(new FixtureHandler(Compress("id\tname\n1\tone\n2\ttwo\n"))),
            Options.Create(new ImdbImportOptions { BatchSize = 1 }), NullLogger<ImdbTsvProvider>.Instance);
        using var cancellation = new CancellationTokenSource();
        var callbackCount = 0;

        var run = provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, ct) =>
            {
                callbackCount++;
                ct.ThrowIfCancellationRequested();
                cancellation.Cancel();
                return Task.CompletedTask;
            }, cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
        callbackCount.Should().Be(1);
    }

    [Fact]
    public async Task CancellationDuringDelayedResponseBodyReadStopsTheFeed()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ImdbTsvProvider(new HttpClient(new DelayedBodyHandler(entered)),
            Options.Create(new ImdbImportOptions()), NullLogger<ImdbTsvProvider>.Instance);
        using var cancellation = new CancellationTokenSource();
        var run = provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask, cancellation.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidUtf8IsASanitizedStrictFeedRejection(bool utf16Preamble)
    {
        var bytes = utf16Preamble
            ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("id\tname\n1\tone\n")).ToArray()
            : Encoding.UTF8.GetBytes("id\tname\n1\t").Concat(new byte[] { 0xc3, 0x28 }).ToArray();
        var provider = Create(Compress(bytes));
        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        var failure = await run.Should().ThrowAsync<InvalidDataException>();
        failure.Which.Message.Should().Be("IMDb feed contains invalid UTF-8.");
        failure.Which.InnerException.Should().BeNull("decoder exceptions can contain rejected bytes");
    }

    [Fact]
    public async Task Utf8PreambleRemainsAcceptedByTheStrictDecoder()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("id\tname\n1\tone\n")).ToArray();
        var provider = Create(Compress(bytes));
        var result = await provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);

        result.ParsedRows.Should().Be(1);
    }

    [Fact]
    public async Task BadGzipCrcDoesNotReportCompleteIngestion()
    {
        var bytes = Compress("id\tname\n1\tone\n");
        bytes[^8] ^= 1;
        await RunInvalidEnvelopeAsync(bytes);
    }

    [Fact]
    public async Task MidDeflateTruncationDoesNotReportCompleteIngestion()
    {
        var bytes = Compress($"id\tname\n1\t{new string('x', 4096)}\n");
        var truncated = bytes[..^9].Concat(bytes[^8..]).ToArray();
        await RunInvalidEnvelopeAsync(truncated);
    }

    [Fact]
    public async Task ValidRowsWithoutTheGzipTrailerRemainIncomplete()
    {
        var bytes = Compress("id\tname\n1\tone\n2\ttwo\n");
        await RunInvalidEnvelopeAsync(bytes[..^8]);
    }

    [Fact]
    public async Task SyntheticOneAndTenMiBFeedsExerciseTheProductionFileParserWhenOptedIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_RUN_IMDB_PARSER_BENCHMARK"), "1", StringComparison.Ordinal))
            return;

        foreach (var targetBytes in new[] { 1 * 1024 * 1024, 10 * 1024 * 1024 })
        {
            var source = BuildSyntheticTsv(targetBytes);
            var compressed = Compress(source);
            var handler = new FixtureHandler(compressed);
            var provider = new ImdbTsvProvider(new HttpClient(handler),
                Options.Create(new ImdbImportOptions
                {
                    MaxCompressedBytesPerFeed = 32L * 1024 * 1024,
                    MaxDecompressedBytesPerFeed = 16L * 1024 * 1024,
                    MaxRowsPerFeed = 100_000
                }), NullLogger<ImdbTsvProvider>.Instance);
            long acceptedRows = 0;
            var stopwatch = Stopwatch.StartNew();

            var result = await provider.RunBatchImportAsync("https://fixture.invalid/synthetic.gz", ["id", "name", "category"],
                (columns, _, _) => columns[1], (batch, _) =>
                {
                    acceptedRows += batch.Count;
                    return Task.CompletedTask;
                });

            stopwatch.Stop();
            result.DecompressedBytes.Should().Be(Encoding.UTF8.GetByteCount(source));
            result.RowsRead.Should().Be(acceptedRows);
            handler.Calls.Should().Be(1);
            output.WriteLine("IMDb parser benchmark: target={0} bytes, compressed={1} bytes, rows={2}, elapsed={3:F1} ms, temp-file=async FileStream, gzip-boundary=one-byte reads.",
                targetBytes, result.CompressedBytes, result.RowsRead, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [Fact]
    public async Task ParserReportsOneBasedLongLineNumbersWithoutIncludingRawRows()
    {
        var provider = Create(Compress("id\tname\n1\tone\n2\ttwo\n"));
        var lineNumbers = new List<long>();

        await provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, lineNumber, _) =>
            {
                lineNumbers.Add(lineNumber);
                return columns[0];
            }, (_, _) => Task.CompletedTask);

        lineNumbers.Should().Equal(2L, 3L);
    }

    [Fact]
    public async Task DelayedFixtureObservesCancellationDuringHttpRead()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ImdbTsvProvider(new HttpClient(new DelayedHandler(entered)),
            Options.Create(new ImdbImportOptions()), NullLogger<ImdbTsvProvider>.Instance);
        using var cancellation = new CancellationTokenSource();
        var run = provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask, cancellation.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
    }

    private async Task RunInvalidEnvelopeAsync(byte[] bytes)
    {
        var provider = Create(bytes);
        var run = () => provider.RunBatchImportAsync("https://fixture.invalid/feed.gz", ["id", "name"],
            (columns, _, _) => columns[0], (_, _) => Task.CompletedTask);
        await run.Should().ThrowAsync<InvalidDataException>();
    }

    private static ImdbTsvProvider Create(byte[] bytes) => Create(new FixtureHandler(bytes));

    private static ImdbTsvProvider Create(FixtureHandler handler) => new(new HttpClient(handler),
        Options.Create(new ImdbImportOptions { BatchSize = 2 }), NullLogger<ImdbTsvProvider>.Instance);

    private static byte[] Compress(string content) => Compress(Encoding.UTF8.GetBytes(content));

    private static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(content);
        return output.ToArray();
    }

    private static string BuildSyntheticTsv(int targetBytes)
    {
        var content = new StringBuilder(targetBytes);
        content.Append("id\tname\tcategory\n");
        var categories = new[] { "movie", "series", "short", "video", "other" };
        var row = 0;
        while (content.Length < targetBytes)
        {
            var id = row.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            var category = categories[row % categories.Length];
            var fixedCharacters = id.Length + 1 + 1 + category.Length + 1;
            var remaining = targetBytes - content.Length;
            if (remaining <= fixedCharacters + 1) break;
            var variableLength = Math.Min(4096, remaining - fixedCharacters);
            variableLength = Math.Min(variableLength, 32 + ((row * 313) % 4064));
            var name = BuildVariedName(row, variableLength);
            content.Append(id).Append('\t').Append(name).Append('\t').Append(category).Append('\n');
            row++;
        }
        return content.ToString();
    }

    private static string BuildVariedName(int row, int length)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var state = unchecked((uint)row + 0x9e3779b9);
        var name = new char[length];
        for (var index = 0; index < name.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            name[index] = alphabet[(int)(state % alphabet.Length)];
        }
        return new string(name);
    }

    private sealed class FixtureHandler(byte[] bytes, long? advertisedLength = null) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            request.RequestUri!.Host.Should().Be("fixture.invalid");
            Calls++;
            var content = new ByteArrayContent(bytes);
            if (advertisedLength.HasValue) content.Headers.ContentLength = advertisedLength;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class DelayedHandler(TaskCompletionSource<bool> entered) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new OperationCanceledException(ct);
        }
    }

    private sealed class DelayedBodyHandler(TaskCompletionSource<bool> entered) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            request.RequestUri!.Host.Should().Be("fixture.invalid");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new DelayedBodyStream(entered))
            });
        }
    }

    private sealed class DelayedBodyStream(TaskCompletionSource<bool> entered) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            entered.TrySetResult(true);
            return WaitForCancellationAsync(cancellationToken);
        }
        private static async ValueTask<int> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
