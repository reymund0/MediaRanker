using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class CatalogImportJobTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitStartupFinishesBeforeEitherDailySchedule(bool leaseAvailable)
    {
        var provider = new CompletedScanProvider(leaseAvailable);
        var client = new Mock<IIgdbClient>(MockBehavior.Strict);
        var igdbOptions = Options.Create(new IgdbOptions { ImportEnabled = true });
        using var services = new ServiceCollection().AddSingleton<IIgdbImportProvider>(provider)
            .AddSingleton(client.Object).AddSingleton(igdbOptions)
            .AddSingleton(Options.Create(new ArtworkOptions()))
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<IgdbImportService>>(NullLogger<IgdbImportService>.Instance)
            .AddScoped<IgdbImportService>().BuildServiceProvider();
        var bootstrap = new CatalogBootstrapOptions
        {
            Provider = "igdb", MaxHttpAttempts = 10, MaxAdmissionRows = 100, MaxSeconds = 10
        };
        var gate = new CatalogScheduleGate(bootstrap);
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        using var imdb = new ImdbImportJob(scopes, Options.Create(new ImdbImportOptions { Enabled = true }),
            bootstrap, gate, TimeProvider.System, NullLogger<ImdbImportJob>.Instance);
        using var igdb = new IgdbImportJob(scopes, igdbOptions, bootstrap, gate, TimeProvider.System,
            NullLogger<IgdbImportJob>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await imdb.StartAsync(deadline.Token);
        await igdb.StartAsync(deadline.Token);
        (await gate.WaitForSchedulesAsync(deadline.Token)).Should().Be(leaseAvailable);
        await igdb.StopAsync(deadline.Token);
        await imdb.StopAsync(deadline.Token);

        provider.Acquisitions.Should().Be(1, "a lease-busy or completed bootstrap must not automatically start another invocation");
        provider.Releases.Should().Be(leaseAvailable ? 1 : 0);
        client.VerifyNoOtherCalls();
        // No IMDb services are registered: resolving one during bootstrap or
        // catching up a missed daily run would fault the job.
        if (imdb.ExecuteTask is not null) imdb.ExecuteTask.IsFaulted.Should().BeFalse();
        if (igdb.ExecuteTask is not null) igdb.ExecuteTask.IsFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduledIgdbAdmissionBlockStopsLaterDaysAndLeavesSharedScheduleGateOpen()
    {
        var provider = new AdmissionBlockedProvider();
        var client = new Mock<IIgdbClient>(MockBehavior.Strict);
        var igdbOptions = Options.Create(new IgdbOptions { ImportEnabled = true, ScheduleHourUtc = 3 });
        using var services = new ServiceCollection().AddSingleton<IIgdbImportProvider>(provider)
            .AddSingleton(client.Object).AddSingleton(igdbOptions)
            .AddSingleton(Options.Create(new ArtworkOptions()))
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<IgdbImportService>>(NullLogger<IgdbImportService>.Instance)
            .AddScoped<IgdbImportService>().BuildServiceProvider();
        var bootstrap = new CatalogBootstrapOptions();
        var gate = new CatalogScheduleGate(bootstrap);
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero));
        using var job = new IgdbImportJob(services.GetRequiredService<IServiceScopeFactory>(), igdbOptions,
            bootstrap, gate, clock, NullLogger<IgdbImportJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await clock.WaitForTimerCountAsync(1, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1));
        await provider.AdmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await job.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        provider.Acquisitions.Should().Be(1);
        provider.AdmissionCalls.Should().Be(1);
        provider.Releases.Should().Be(1);
        client.VerifyNoOtherCalls();
        (await gate.WaitForSchedulesAsync(CancellationToken.None)).Should().BeTrue(
            "a scheduled IGDB admission block pauses that job without closing the shared gate for IMDb");

        clock.Advance(TimeSpan.FromDays(1));
        provider.Acquisitions.Should().Be(1, "the blocked IGDB job must not start another daily invocation");
        await job.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledImdbInvalidFeedStopsPermanentlyForThisProcess(bool invalidUtf8)
    {
        var feed = invalidUtf8
            ? Encoding.UTF8.GetBytes("tconst\taverageRating\tnumVotes\n").Concat(new byte[] { 0xc3, 0x28 }).ToArray()
            : Encoding.UTF8.GetBytes("wrong\tfeed\theader\n");
        var handler = new RecordingImdbHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Gzip(feed))
        });
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero));
        using var harness = CreateScheduledImdbJob(ValidImdbOptions(), handler, clock);

        await harness.Job.StartAsync(CancellationToken.None);
        await clock.WaitForTimerCountAsync(1, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1));
        await handler.WaitForRequestCountAsync(1, TimeSpan.FromSeconds(5));
        await harness.Job.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        handler.RequestCount.Should().Be(1);
        harness.ImportProvider.Verify(provider => provider.GetDatabaseUtcNowAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.ImportProvider.Verify(provider => provider.ImportRatingsAsync(
            It.IsAny<List<ImdbRatingTsvRow>>(), It.IsAny<CancellationToken>()), Times.Never);
        (await harness.Schedules.WaitForSchedulesAsync(CancellationToken.None)).Should().BeTrue();

        clock.Advance(TimeSpan.FromDays(1));
        handler.RequestCount.Should().Be(1, "a malformed feed blocks further IMDb attempts until a deliberate restart");
        await harness.Job.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ScheduledImdbTransientHttpFailureWaitsUntilTheNextDailySchedule()
    {
        var handler = new RecordingImdbHandler(_ => throw new HttpRequestException("fixture transient failure"));
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero));
        using var harness = CreateScheduledImdbJob(ValidImdbOptions(), handler, clock);

        await harness.Job.StartAsync(CancellationToken.None);
        await clock.WaitForTimerCountAsync(1, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1));
        await handler.WaitForRequestCountAsync(1, TimeSpan.FromSeconds(5));
        await clock.WaitForTimerCountAsync(2, TimeSpan.FromSeconds(5));
        handler.RequestCount.Should().Be(1, "the transient failure is deferred instead of retried immediately");
        harness.Job.ExecuteTask!.IsCompleted.Should().BeFalse();

        clock.Advance(TimeSpan.FromDays(1));
        await handler.WaitForRequestCountAsync(2, TimeSpan.FromSeconds(5));
        await clock.WaitForTimerCountAsync(3, TimeSpan.FromSeconds(5));
        handler.RequestCount.Should().Be(2);
        harness.ImportProvider.Verify(provider => provider.GetDatabaseUtcNowAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        await harness.Job.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ScheduledImdbFailureLogsSanitizedCommittedImportProgress()
    {
        var handler = new RecordingImdbHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/ratings", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Gzip(Encoding.UTF8.GetBytes(
                        "tconst\taverageRating\tnumVotes\ntt0000001\t8.0\t1000\n")))
                };
            throw new HttpRequestException("raw-private-marker");
        });
        var logger = new RecordingLogger<ImdbImportJob>();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero));
        using var harness = CreateScheduledImdbJob(ValidImdbOptions(), handler, clock, logger);
        harness.ImportProvider.Setup(provider => provider.ImportRatingsAsync(
                It.IsAny<List<ImdbRatingTsvRow>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbImportResult(1, 0));

        await harness.Job.StartAsync(CancellationToken.None);
        await clock.WaitForTimerCountAsync(1, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1));
        await handler.WaitForRequestCountAsync(2, TimeSpan.FromSeconds(5));
        await clock.WaitForTimerCountAsync(2, TimeSpan.FromSeconds(5));

        var progress = logger.Messages.Single(message => message.Contains("failure category HttpRequestException", StringComparison.Ordinal));
        progress.Should().Contain("stage basics")
            .And.Contain("HTTP attempts 2")
            .And.Contain("elapsed ")
            .And.Contain("batches committed 1")
            .And.Contain("rows affected 1");
        logger.Messages.Should().NotContain(message => message.Contains("raw-private-marker", StringComparison.Ordinal));
        harness.ImportProvider.Verify(provider => provider.DeleteStaleRatingsAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        await harness.Job.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ScheduledImdbLoadFailureLogsPriorLoadAndImportProgress()
    {
        var feeds = new Dictionary<string, byte[]>
        {
            ["/ratings"] = Gzip(Encoding.UTF8.GetBytes("tconst\taverageRating\tnumVotes\ntt0000001\t8.0\t1000\n")),
            ["/basics"] = Gzip(Encoding.UTF8.GetBytes("tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres\n"
                + "tt0000001\tmovie\tTitle\tTitle\t0\t2020\t\\N\t90\tDrama\n")),
            ["/episodes"] = Gzip(Encoding.UTF8.GetBytes("tconst\tparentTconst\tseasonNumber\tepisodeNumber\ntt0000002\ttt0000003\t\\N\t1\n"))
        };
        var handler = new RecordingImdbHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(feeds[request.RequestUri!.AbsolutePath])
        });
        var logger = new RecordingLogger<ImdbImportJob>();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero));
        var loadProvider = new Mock<IImdbLoadProvider>(MockBehavior.Strict);
        loadProvider.Setup(provider => provider.LoadNonSeriesMediaBatchAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int _, string? after, int _, CancellationToken _) => after is null
                ? Task.FromResult(new ImdbLoadBatchResult(1, "tt0000001", true))
                : Task.FromException<ImdbLoadBatchResult>(new InvalidOperationException("raw-load-marker")));
        using var harness = CreateScheduledImdbJob(ValidImdbOptions(), handler, clock, logger, loadProvider.Object);
        harness.ImportProvider.Setup(provider => provider.ImportRatingsAsync(
                It.IsAny<List<ImdbRatingTsvRow>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbImportResult(1, 0));
        harness.ImportProvider.Setup(provider => provider.ImportBasicsAsync(
                It.IsAny<List<ImdbTsvRow>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbImportResult(1, 0));
        harness.ImportProvider.Setup(provider => provider.ImportEpisodesAsync(
                It.IsAny<List<ImdbEpisodeTsvRow>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbImportResult(1, 0));
        harness.ImportProvider.Setup(provider => provider.DeleteStaleRatingsAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        harness.ImportProvider.Setup(provider => provider.DeleteFutureImportsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        harness.ImportProvider.Setup(provider => provider.DeleteTvPilotImportsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        harness.ImportProvider.Setup(provider => provider.DeleteOrphanEpisodesBatchAsync(
                It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImdbCleanupBatchResult(0, null, false));

        await harness.Job.StartAsync(CancellationToken.None);
        await clock.WaitForTimerCountAsync(1, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1));
        await handler.WaitForRequestCountAsync(3, TimeSpan.FromSeconds(5));
        await clock.WaitForTimerCountAsync(2, TimeSpan.FromSeconds(5));

        var progress = logger.Messages.Single(message => message.Contains("failure category InvalidOperationException", StringComparison.Ordinal));
        progress.Should().Contain("stage load:non-series")
            .And.Contain("HTTP attempts 3")
            .And.Contain("rows affected 3")
            .And.Contain("load affected 1");
        logger.Messages.Should().NotContain(message => message.Contains("raw-load-marker", StringComparison.Ordinal));
        await harness.Job.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DisabledStartupNeverResolvesAnImporter()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var bootstrap = new CatalogBootstrapOptions();
        var gate = new CatalogScheduleGate(bootstrap);
        using var job = new IgdbImportJob(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IgdbOptions()), bootstrap, gate, TimeProvider.System, NullLogger<IgdbImportJob>.Instance);
        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);
        job.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class CompletedScanProvider(bool leaseAvailable) : IIgdbImportProvider
    {
        public int Acquisitions { get; private set; }
        public int Releases { get; private set; }
        public Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan duration, CancellationToken ct)
        {
            Acquisitions++;
            return Task.FromResult(leaseAvailable ? new IgdbImportLease(new IgdbImportState { BootstrapCompleted = true }, Guid.NewGuid(), duration) : null);
        }
        public Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? max, DateTimeOffset? after,
            DateTimeOffset? before, DateTimeOffset now, CancellationToken ct) => throw new InvalidOperationException("Completed bootstrap must be admission-only.");
        public Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long id, CancellationToken ct)
            => throw new InvalidOperationException("No page is expected.");
        public Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct) => throw new InvalidOperationException("No scan is expected.");
        public Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct) { Releases++; return Task.CompletedTask; }
        public Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> types, DateTimeOffset now,
            TimeSpan positive, TimeSpan negative, CancellationToken ct) => throw new InvalidOperationException("The job must use bounded admission.");
        public Task<IgdbAdmissionResult> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> types, DateTimeOffset now,
            TimeSpan positive, TimeSpan negative, ImportWorkUnitBudget budget, CancellationToken ct)
            => Task.FromResult(new IgdbAdmissionResult(0, 0, 0, true, false));
    }

    private static ImdbJobHarness CreateScheduledImdbJob(
        ImdbImportOptions options, RecordingImdbHandler handler, ManualTimeProvider clock,
        ILogger<ImdbImportJob>? logger = null, IImdbLoadProvider? loadProvider = null)
    {
        var imdbOptions = Options.Create(options);
        var importProvider = new Mock<IImdbImportProvider>(MockBehavior.Strict);
        importProvider.Setup(provider => provider.GetDatabaseUtcNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
        var httpClient = new HttpClient(handler);
        var parser = new ImdbTsvProvider(httpClient, imdbOptions, NullLogger<ImdbTsvProvider>.Instance);
        var registrations = new ServiceCollection()
            .AddSingleton<IImdbImportProvider>(importProvider.Object)
            .AddSingleton(parser)
            .AddSingleton(imdbOptions)
            .AddScoped<ImdbImportService>(scope => new ImdbImportService(
                parser,
                scope.GetRequiredService<IServiceScopeFactory>(),
                imdbOptions,
                NullLogger<ImdbImportService>.Instance))
            ;
        if (loadProvider is not null)
            registrations.AddScoped(_ => new ImdbLoadService(loadProvider, imdbOptions, NullLogger<ImdbLoadService>.Instance));
        var services = registrations.BuildServiceProvider();
        var bootstrap = new CatalogBootstrapOptions();
        var schedules = new CatalogScheduleGate(bootstrap);
        var job = new ImdbImportJob(services.GetRequiredService<IServiceScopeFactory>(), imdbOptions,
            bootstrap, schedules, clock, logger ?? NullLogger<ImdbImportJob>.Instance);
        return new ImdbJobHarness(services, httpClient, job, schedules, importProvider);
    }

    private static ImdbImportOptions ValidImdbOptions() => new()
    {
        Enabled = true,
        ScheduleHourUtc = 3,
        DatasetUrl = "https://fixture.invalid/basics",
        EpisodesDatasetUrl = "https://fixture.invalid/episodes",
        RatingsDatasetUrl = "https://fixture.invalid/ratings",
        BatchSize = 1,
        MaxHttpAttempts = 3,
        MaxCompressedBytesPerFeed = 1024,
        MaxTemporaryDiskBytes = 4096,
        MaxDecompressedBytesPerFeed = 4096,
        MaxWholeSessionSeconds = 60,
        MaxRowsPerFeed = 10,
        MaxLineCharacters = 1024,
        MaxLoadRowsPerUnit = 1,
        MaxCleanupRowsPerUnit = 1,
        MaxStatementSeconds = 5
    };

    private static byte[] Gzip(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(content, 0, content.Length);
        }
        return output.ToArray();
    }

    private sealed class AdmissionBlockedProvider : IIgdbImportProvider
    {
        private readonly IgdbImportLease lease = new(new IgdbImportState(), Guid.NewGuid(), TimeSpan.FromMinutes(2));
        private int acquisitions;
        private int admissionCalls;
        private int releases;

        public int Acquisitions => Volatile.Read(ref acquisitions);
        public int AdmissionCalls => Volatile.Read(ref admissionCalls);
        public int Releases => Volatile.Read(ref releases);
        public TaskCompletionSource<bool> AdmissionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan duration, CancellationToken ct)
        {
            Interlocked.Increment(ref acquisitions);
            return Task.FromResult<IgdbImportLease?>(lease);
        }
        public Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? max, DateTimeOffset? after,
            DateTimeOffset? before, DateTimeOffset now, CancellationToken ct) => throw new InvalidOperationException("Admission block must stop before upstream work.");
        public Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long id, CancellationToken ct)
            => throw new InvalidOperationException("Admission block must stop before upstream work.");
        public Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct)
            => throw new InvalidOperationException("Admission block must stop before upstream work.");
        public Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct)
        {
            Interlocked.Increment(ref releases);
            return Task.CompletedTask;
        }
        public Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> types, DateTimeOffset now,
            TimeSpan positive, TimeSpan negative, CancellationToken ct)
            => throw new InvalidOperationException("The scheduled job must use bounded admission.");
        public Task<IgdbAdmissionResult> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> types,
            DateTimeOffset now, TimeSpan positive, TimeSpan negative, ImportWorkUnitBudget budget, CancellationToken ct)
        {
            Interlocked.Increment(ref admissionCalls);
            AdmissionEntered.TrySetResult(true);
            return Task.FromResult(new IgdbAdmissionResult(0, 0, 3, false, true, "fixture-blocked", "1-3"));
        }
    }

    private sealed record ImdbJobHarness(
        ServiceProvider Services,
        HttpClient HttpClient,
        ImdbImportJob Job,
        CatalogScheduleGate Schedules,
        Mock<IImdbImportProvider> ImportProvider) : IDisposable
    {
        public void Dispose()
        {
            Job.Dispose();
            HttpClient.Dispose();
            Services.Dispose();
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class RecordingImdbHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource<bool>> waiters = new();
        private int requestCount;
        public int RequestCount => Volatile.Read(ref requestCount);

        public async Task WaitForRequestCountAsync(int expected, TimeSpan timeout)
        {
            if (RequestCount >= expected) return;
            var waiter = waiters.GetOrAdd(expected, _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            if (RequestCount >= expected) waiter.TrySetResult(true);
            await waiter.Task.WaitAsync(timeout);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref requestCount);
            if (waiters.TryGetValue(count, out var waiter)) waiter.TrySetResult(true);
            try { return Task.FromResult(responder(request)); }
            catch (Exception exception) { return Task.FromException<HttpResponseMessage>(exception); }
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        private readonly object sync = new();
        private readonly List<ManualTimer> timers = [];
        private readonly SemaphoreSlim timerCreated = new(0);
        private DateTimeOffset currentUtc = initialUtc;
        private int timerCount;

        public override DateTimeOffset GetUtcNow()
        {
            lock (sync) return currentUtc;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            lock (sync) timers.Add(timer);
            Interlocked.Increment(ref timerCount);
            timerCreated.Release();
            return timer;
        }

        public async Task WaitForTimerCountAsync(int expected, TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            while (Volatile.Read(ref timerCount) < expected)
                await timerCreated.WaitAsync(cancellation.Token);
        }

        public void Advance(TimeSpan amount)
        {
            ManualTimer[] currentTimers;
            DateTimeOffset now;
            lock (sync)
            {
                currentUtc += amount;
                now = currentUtc;
                currentTimers = timers.ToArray();
            }
            foreach (var timer in currentTimers) timer.FireIfDue(now);
        }

        private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private readonly object sync = new();
            private DateTimeOffset? dueAt;
            private TimeSpan period;
            private bool disposed;

            public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
            {
                lock (sync)
                {
                    if (disposed) return false;
                    dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
                    period = newPeriod;
                    return true;
                }
            }

            public void Dispose()
            {
                lock (sync) disposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                lock (sync)
                {
                    if (disposed || dueAt is not { } due || due > now) return;
                    if (period <= TimeSpan.Zero || period == Timeout.InfiniteTimeSpan) dueAt = null;
                    else dueAt = now + period;
                }
                callback(state);
            }
        }
    }
}
