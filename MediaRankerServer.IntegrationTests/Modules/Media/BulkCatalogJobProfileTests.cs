using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MediaRankerServer.IntegrationTests.Infrastructure;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MediaRankerServer.IntegrationTests.Modules.Media;

/// <summary>
/// Opt-in calibration of the actual hosted IGDB job against fixture PostgreSQL
/// and local fake provider HTTP. A normal integration run does no profile work.
/// </summary>
public sealed class BulkCatalogJobProfileTests(
    PostgresContainerFixture postgresFixture,
    LocalStackContainerFixture localStackFixture)
    : IntegrationTestBase(postgresFixture, localStackFixture)
{
    private const int StagingRows = 50_000;
    private const int SessionAdmissionRows = 5_000;
    private const int UnitAdmissionRows = 500;
    private const int StatementSeconds = 15;
    private const long MaxRssBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxDatabaseBytes = 1024L * 1024 * 1024;
    private const int MeasuredRuns = 5;
    private static readonly TimeSpan WholeRunLimit = TimeSpan.FromMinutes(15);
    private static readonly IReadOnlySet<string> SupportedTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Main game", "Remake", "Remaster" };

    [Fact]
    public async Task OptInActualJobProfile_RecordsBoundedRecoveryAndMixedWorkloadEvidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEDIARANKER_BULK_JOB_PROFILE"), "1", StringComparison.Ordinal))
            return;

        var outputPath = Environment.GetEnvironmentVariable("MEDIARANKER_BULK_JOB_PROFILE_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathFullyQualified(outputPath))
            throw new InvalidOperationException("MEDIARANKER_BULK_JOB_PROFILE_OUTPUT must be an absolute sanitized JSON artifact path.");

        var arm = (Environment.GetEnvironmentVariable("MEDIARANKER_BULK_JOB_PROFILE_ARM") ?? "all").Trim().ToLowerInvariant();
        if (arm is not ("all" or "backlog" or "http" or "cancellation"))
            throw new InvalidOperationException("MEDIARANKER_BULK_JOB_PROFILE_ARM must be all, backlog, http, or cancellation.");

        using var wholeRun = new CancellationTokenSource(WholeRunLimit);
        var startedAt = DateTimeOffset.UtcNow;
        var samples = new List<JobProfileSample>();
        var drainSessions = new List<DrainSessionSample>();
        var mixed = new List<MixedWorkloadSample>();
        CancellationSample? cancellation = null;
        var baselineDatabaseBytes = await ReadDatabaseSizeBytesAsync(await FixtureConnectionStringAsync(), wholeRun.Token);
        var coverageGaps = new List<string>
        {
            "All catalog rows and SQL run in the PostgreSQL Testcontainers fixture; all provider traffic uses the in-process fake handler. The original database and live provider endpoints are not opened.",
            "PostgreSQL database size is measured separately from the timed job; temporary download-disk usage is not applicable to these IGDB-only scenarios.",
            "Transactions report p50/p95/max for completed explicit transactions only; implicit provider transactions and in-flight transactions are outside that sample.",
            "This staged eligible-row profile supports a local backlog forecast only; it does not estimate sparse IGDB ID coverage or IMDb full-feed readiness.",
            "This file records actual job/query timings and request totals; query plans remain available through the existing isolated eligibility-plan measurement arm."
        };

        try
        {
            var connectionString = await FixtureConnectionStringAsync();
            if (arm is "all" or "backlog")
            {
                await MeasureBacklogAsync(connectionString, samples, drainSessions, wholeRun.Token);
            }
            if (arm is "all" or "http")
                mixed.Add(await MeasureHttpAndArtworkAsync(connectionString, wholeRun.Token));
            if (arm is "all" or "cancellation")
                cancellation = await MeasureCancellationAsync(connectionString, wholeRun.Token);

            AssertResourceEnvelope(samples, drainSessions, mixed, cancellation);
        }
        finally
        {
            var finishedAt = DateTimeOffset.UtcNow;
            var finalDatabaseBytes = await ReadDatabaseSizeBytesAsync(await FixtureConnectionStringAsync(), CancellationToken.None);
            finalDatabaseBytes.Should().BeLessThanOrEqualTo(MaxDatabaseBytes,
                "the disposable fixture database must stay within the existing 1 GiB measurement envelope");
            await WriteArtifactAsync(outputPath, new JobProfileArtifact(
                startedAt,
                finishedAt,
                arm,
                "fixture PostgreSQL (Testcontainers) + local fake IGDB/Twitch HTTP",
                "dotnet test --filter FullyQualifiedName~BulkCatalogJobProfileTests",
                BuildLimits(),
                baselineDatabaseBytes,
                finalDatabaseBytes,
                samples,
                drainSessions,
                mixed,
                cancellation,
                coverageGaps));
        }
    }

    private async Task MeasureBacklogAsync(
        string connectionString, List<JobProfileSample> samples, List<DrainSessionSample> drain, CancellationToken ct)
    {
        ProfileRuntime? drainRuntime = null;
        FixtureState? measuredStart = null;
        try
        {
            for (var index = 0; index <= MeasuredRuns; index++)
            {
                var warmup = index == 0;
                await SeedCompletedScanBacklogAsync(connectionString, StagingRows, ct);
                var databaseBefore = await ReadDatabaseSizeBytesAsync(connectionString, ct);
                var fixtureAge = await ReadOldestEligibleAgeAsync(connectionString, ct);
                var before = await ReadFixtureStateAsync(connectionString, ct);
                if (!warmup && index == MeasuredRuns)
                {
                    drainRuntime = new ProfileRuntime(connectionString);
                    measuredStart = before;
                }

                var keepForDrain = !warmup && index == MeasuredRuns;
                var runtime = keepForDrain ? drainRuntime! : new ProfileRuntime(connectionString);
                try
                {
                    var run = await RunJobAsync(runtime, ct);
                    var after = await ReadFixtureStateAsync(connectionString, ct);
                    var databaseAfter = await ReadDatabaseSizeBytesAsync(connectionString, ct);
                    var sample = BuildSample(warmup ? "warmup" : $"backlog-{index}", "backlog", run,
                        before, after, databaseBefore, databaseAfter, fixtureAge);
                    samples.Add(sample);

                    after.MediaCount.Should().Be(SessionAdmissionRows,
                        "the job admits exactly one 5,000-row session from the 50,000-row eligible fixture");
                    run.SessionAdmissionRows.Should().Be(SessionAdmissionRows);
                    run.SessionHttpAttempts.Should().Be(0);
                    run.FakeHttpAttempts.Should().Be(0);
                    run.ServiceScopes.Should().Be(10, "each of ten 500-row work units resolves a fresh service scope");
                    run.SchedulesResumed.Should().BeFalse("the admission cap pauses both catalog schedules for this process");
                    run.CatalogReady.Should().BeFalse();
                    run.StopReason.Should().Be(nameof(ImportStopReason.SessionAdmissionLimit));
                    run.Progress.Should().HaveCount(10);
                    run.Progress.Take(9).Should().OnlyContain(x =>
                        x.GetString("StopReason") == nameof(ImportStopReason.UnitAdmissionLimit));
                    run.AdmissionRemaining.Should().Be(StagingRows - SessionAdmissionRows);
                    run.YieldIntervalsMilliseconds.Should().HaveCount(9);
                    run.YieldIntervalsMilliseconds.Should().OnlyContain(x => x == 1_000);
                    run.AdmissionRetryCount.Should().Be(0);
                    run.SqlFailures.Should().Be(0);
                    run.MaximumSqlMilliseconds.Should().BeLessThanOrEqualTo(StatementSeconds * 1_000);
                    databaseAfter.Should().BeLessThanOrEqualTo(MaxDatabaseBytes);
                }
                finally
                {
                    if (!keepForDrain)
                        await runtime.DisposeAsync();
                }
            }

            samples.Where(x => x.Name != "warmup").Select(x => x.AdmissionRowsLoaded).Should()
                .OnlyContain(x => x == SessionAdmissionRows);
            var measured = samples.Where(x => x.Name != "warmup").ToArray();
            measured.Should().HaveCount(MeasuredRuns);

            // The final measured fixture has 45,000 eligible rows left. Start a
            // new finite job/session each time, as an operator would on relaunch.
            drainRuntime.Should().NotBeNull();
            measuredStart.Should().NotBeNull();
            var preDrainIdentity = await ReadSampleMediaIdentityAsync(connectionString, ct);
            for (var session = 1; session <= 9; session++)
            {
                var run = await RunJobAsync(drainRuntime!, ct);
                var state = await ReadFixtureStateAsync(connectionString, ct);
                var admitted = run.AdmittedRows;
                admitted.Should().Be(SessionAdmissionRows);
                run.SessionAdmissionRows.Should().Be(SessionAdmissionRows);
                run.SessionHttpAttempts.Should().Be(0);
                run.FakeHttpAttempts.Should().Be(0);
                run.SchedulesResumed.Should().Be(session == 9,
                    "the final relaunch becomes ready only after the final eligible staged row is admitted");
                drain.Add(new DrainSessionSample(session, run.ElapsedMilliseconds, run.ServiceScopes, admitted,
                    run.AdmissionRemaining, run.CatalogReady, state.MediaCount, state.DistinctIgdbExternalIds,
                    run.SqlCommandCount, run.TransactionP50Milliseconds, run.TransactionP95Milliseconds,
                    run.TransactionMaxMilliseconds, run.YieldIntervalsMilliseconds, run.Progress));
            }

            var finalState = await ReadFixtureStateAsync(connectionString, ct);
            finalState.MediaCount.Should().Be(StagingRows);
            finalState.DistinctIgdbExternalIds.Should().Be(StagingRows,
                "the ten capped process-local sessions preserve a unique IGDB domain identity per staging row");
            foreach (var identity in preDrainIdentity)
                finalState.MediaIdsByExternalId.Should().Contain(identity);
            drain[^1].CatalogReady.Should().BeTrue();
            drain[^1].AdmissionRemaining.Should().Be(0);
        }
        finally
        {
            if (drainRuntime is not null)
                await drainRuntime.DisposeAsync();
        }
    }

    private async Task<MixedWorkloadSample> MeasureHttpAndArtworkAsync(string connectionString, CancellationToken ct)
    {
        await SeedEmptyImportStateAsync(connectionString, bootstrapCompleted: false, ct);
        await using var runtime = new ProfileRuntime(connectionString);
        var run = await RunJobAsync(runtime, ct, async progressCt =>
        {
            await runtime.Logs.WaitForProgressAsync(1, progressCt);
            var firstProgress = runtime.Logs.ProgressSnapshot()[0];
            var artwork = await Task.WhenAll(new[] { "101", "102", "103" }
                .Select(id => runtime.Client.GetCoverAsync(id, progressCt)));
            artwork.Should().HaveCount(3);
            artwork.Select(x => x.ImagePath).Should().Equal("profile-cover-101", "profile-cover-102", "profile-cover-103");
            var secondProgress = runtime.Logs.WaitForProgressAsync(2, progressCt);
            var artworkSends = runtime.Http.Attempts.Where(x => x.Operation == "artwork").ToArray();
            artworkSends.Should().HaveCount(3);
            artworkSends.Should().OnlyContain(x => x.StartedAt >= firstProgress.TimestampUtc,
                "the calls are initiated after the first import unit completes, during its configured yield");
            secondProgress.IsCompleted.Should().BeFalse("the next import unit must still be inside its one-second yield while artwork calls finish");
        });

        var state = await ReadFixtureStateAsync(connectionString, ct);
        var sends = runtime.Http.Attempts;
        var importSendCount = sends.Count(x => x.Operation != "artwork");
        var artworkSendCount = sends.Count(x => x.Operation == "artwork");
        var minPacing = MinimumProviderSpacingMilliseconds(sends);
        minPacing.Should().BeGreaterThanOrEqualTo(225, "the shared four-per-second limiter must space sends, allowing timer precision");
        run.SchedulesResumed.Should().BeTrue();
        run.CatalogReady.Should().BeTrue();
        run.AdmittedRows.Should().Be(201);
        run.AdmissionRemaining.Should().Be(0);
        run.Progress.Select(x => x.GetInt32("Pages")).Sum().Should().Be(3,
            "the fake provider exposes two full 100-game pages and one terminal one-game page");
        run.ServiceScopes.Should().Be(3, "each page unit runs in a fresh job scope");
        importSendCount.Should().Be(run.SessionHttpAttempts,
            "import attempt totals include token/discovery/game-page sends but exclude independent artwork calls");
        artworkSendCount.Should().Be(3);
        runtime.Http.MaxConcurrency.Should().Be(1, "import and artwork share one limiter and one active provider slot");
        run.YieldIntervalsMilliseconds.Should().HaveCount(2);
        run.YieldIntervalsMilliseconds.Should().OnlyContain(x => x == 1_000);
        state.MediaCount.Should().Be(201);
        run.SqlFailures.Should().Be(0);
        run.MaximumSqlMilliseconds.Should().BeLessThanOrEqualTo(StatementSeconds * 1_000);

        return new MixedWorkloadSample(run.ElapsedMilliseconds, run.ServiceScopes, state.MediaCount,
            run.SessionHttpAttempts, importSendCount, artworkSendCount, sends.Count,
            run.FakeHttpFailures, runtime.Http.MaxConcurrency, minPacing,
            run.AdmittedRows, run.AdmissionRemaining, run.CatalogReady, run.SchedulesResumed,
            run.SqlCommandCount, run.SqlFailures, run.TransactionP50Milliseconds,
            run.TransactionP95Milliseconds, run.TransactionMaxMilliseconds,
            run.MinimumLeaseHeadroomMilliseconds, run.MaximumRenewalIntervalMilliseconds,
            run.YieldIntervalsMilliseconds, run.Progress, sends)
        {
            PeakWorkingSetBytes = run.Resources.PeakWorkingSetBytes,
            MaximumSqlMilliseconds = run.MaximumSqlMilliseconds
        };
    }

    private async Task<CancellationSample> MeasureCancellationAsync(string connectionString, CancellationToken ct)
    {
        await SeedEmptyImportStateAsync(connectionString, bootstrapCompleted: false, ct);
        await using var runtime = new ProfileRuntime(connectionString, holdFirstImportPage: true);
        var jobRun = await RunJobAsync(runtime, ct, cancelAtFirstPage: true);
        var state = await ReadFixtureStateAsync(connectionString, ct);
        jobRun.CancellationTimestampUtc.Should().NotBeNull();
        jobRun.CancellationToLeaseReleaseMilliseconds.Should().NotBeNull();
        jobRun.CancellationToLeaseReleaseMilliseconds.Should().BeInRange(0, 30_000);
        jobRun.CancellationToJobCompletionMilliseconds.Should().NotBeNull().And.BeInRange(0, 30_000);
        jobRun.SessionHttpAttempts.Should().BeGreaterThan(0);
        jobRun.SessionHttpAttempts.Should().Be(jobRun.FakeHttpAttempts,
            "the canceled in-flight send still consumes an import attempt");
        jobRun.SchedulesResumed.Should().BeFalse();
        jobRun.Cursor.Should().Be(0L, "a canceled in-flight provider page must not advance the durable cursor");
        state.LastCommittedId.Should().Be(0);
        state.ClaimedUntil.Should().BeNull("cancellation must release the durable lease");
        state.BootstrapCompleted.Should().BeFalse();
        jobRun.ProviderCallsAtCancellation.Should().Be(jobRun.FakeHttpAttempts,
            "no new provider send may start after cancellation is requested");

        return new CancellationSample(jobRun.ElapsedMilliseconds, jobRun.CancellationToJobCompletionMilliseconds,
            jobRun.CancellationToLeaseReleaseMilliseconds, jobRun.ProviderCallsAtCancellation.GetValueOrDefault(),
            jobRun.FakeHttpAttempts, jobRun.SessionHttpAttempts, jobRun.Cursor, state.ClaimedUntil,
            jobRun.SchedulesResumed, jobRun.SqlCommandCount, jobRun.SqlFailures,
            jobRun.TransactionP50Milliseconds, jobRun.TransactionP95Milliseconds,
            jobRun.TransactionMaxMilliseconds, jobRun.Progress, runtime.Http.Attempts)
        {
            PeakWorkingSetBytes = jobRun.Resources.PeakWorkingSetBytes,
            MaximumSqlMilliseconds = jobRun.MaximumSqlMilliseconds
        };
    }

    private async Task<JobRunEvidence> RunJobAsync(ProfileRuntime runtime, CancellationToken ct,
        Func<CancellationToken, Task>? duringFirstYield = null, bool cancelAtFirstPage = false)
    {
        runtime.Logs.Reset();
        runtime.Headroom.Reset();
        var commandStart = runtime.Database.Commands.Count;
        var transactionStart = runtime.Database.TransactionInstrumentation.CompletedDurationsMilliseconds.Count;
        var transactionCountStart = runtime.Database.TransactionsStarted;
        var httpStart = runtime.Http.Attempts.Count;
        var releaseStart = runtime.ReleaseObserver.LastReleaseCompletedUtc;
        var clock = new YieldRecordingTimeProvider();
        var scopes = new CountingScopeFactory(runtime.Root.GetRequiredService<IServiceScopeFactory>());
        var schedules = new CatalogScheduleGate(BootstrapOptions());
        using var job = new IgdbImportJob(scopes, Options.Create(runtime.Options), BootstrapOptions(), schedules,
            clock, runtime.Logs.CreateLogger<IgdbImportJob>());
        var timer = Stopwatch.StartNew();
        DateTimeOffset? cancellationTimestamp = null;
        var callsAtCancellation = 0;
        var cancelledAtPage = false;
        await job.StartAsync(CancellationToken.None);

        try
        {
            var scheduleTask = schedules.WaitForSchedulesAsync(ct);
            if (cancelAtFirstPage)
            {
                await runtime.Http.FirstImportPageStarted.Task.WaitAsync(ct);
                cancellationTimestamp = DateTimeOffset.UtcNow;
                callsAtCancellation = runtime.Http.TotalCalls;
                cancelledAtPage = true;
                await StopJobAsync(job);
            }
            else
            {
                if (duringFirstYield is not null)
                {
                    var firstProgress = runtime.Logs.WaitForProgressAsync(1, ct);
                    var winner = await Task.WhenAny(firstProgress, scheduleTask);
                    if (winner == scheduleTask && !firstProgress.IsCompleted)
                        throw new InvalidOperationException("The job ended before producing its first import progress event.");
                    await duringFirstYield(ct);
                }

                await scheduleTask;
                await StopJobAsync(job);
            }

            var executeTask = job.ExecuteTask ?? throw new InvalidOperationException("The hosted job did not create an execute task.");
            await executeTask.WaitAsync(ct);
            var jobCompletedAt = DateTimeOffset.UtcNow;
            timer.Stop();
            var schedulesResumed = await schedules.WaitForSchedulesAsync(ct);
            var progress = runtime.Logs.ProgressSnapshot();
            var totals = runtime.Logs.SessionTotalsSnapshot();
            totals.Should().NotBeNull("every stopped job reports its cumulative session counters");
            totals!.Values.Should().ContainKeys("SessionId", "HttpAttempts", "AdmissionRows", "ElapsedMs", "@HttpOperations");
            foreach (var entry in progress)
                entry.Values.Should().ContainKeys("Mode", "SessionId", "StopReason", "Pages", "Rows", "Admitted",
                    "UpstreamComplete", "AdmissionRemaining", "CatalogReady", "IncrementalPending", "Cursor",
                    "MaximumId", "AdmissionRetries", "FailingBatch");
            runtime.Logs.Snapshot().Should().OnlyContain(entry =>
                !entry.Message.Contains("fixture-secret") && !entry.Message.Contains("fixture-token"));
            var sends = runtime.Http.Attempts.Skip(httpStart).ToArray();
            var commandRecords = runtime.Database.Commands.Skip(commandStart).ToArray();
            var transactionDurations = runtime.Database.TransactionInstrumentation.CompletedDurationsMilliseconds
                .Skip(transactionStart).ToArray();
            var lastProgress = progress.LastOrDefault();
            var sessionAttempts = totals?.GetInt32("HttpAttempts") ?? 0;
            var sessionAdmission = totals?.GetInt32("AdmissionRows") ?? 0;
            var admitted = progress.Sum(x => x.GetInt32("Admitted"));
            var remaining = lastProgress?.GetNullableInt32("AdmissionRemaining");
            var catalogReady = progress.Any(x => x.GetBoolean("CatalogReady"));
            var cursor = lastProgress?.GetNullableInt64("Cursor");
            var maxSql = commandRecords.Select(x => x.DurationMilliseconds).DefaultIfEmpty(0).Max();
            var txMs = transactionDurations;
            var releaseCompleted = runtime.ReleaseObserver.LastReleaseCompletedUtc;
            var completionMs = cancellationTimestamp is { } canceledAt
                ? (long?)(jobCompletedAt - canceledAt).TotalMilliseconds : null;
            var releaseMs = cancellationTimestamp is { } cancelAt && releaseCompleted is { } released
                && (releaseStart is null || released > releaseStart)
                    ? (long?)(released - cancelAt).TotalMilliseconds : null;
            var rss = CaptureResources();
            maxSql.Should().BeLessThanOrEqualTo(StatementSeconds * 1000);
            if (!cancelAtFirstPage)
            {
                runtime.Headroom.MinimumLeaseHeadroomMilliseconds.Should().NotBeNull().And.BeGreaterThan(0);
                runtime.Headroom.LeaseDurationMilliseconds.Should().BeGreaterThanOrEqualTo(
                    2 * (runtime.Headroom.MaximumRenewalIntervalMilliseconds ?? 0));
                commandRecords.Should().OnlyContain(x => x.Succeeded);
                sends.Should().OnlyContain(x => x.Succeeded);
            }

            return new JobRunEvidence(
                (long)timer.Elapsed.TotalMilliseconds,
                scopes.Count,
                sessionAttempts,
                sessionAdmission,
                admitted,
                remaining,
                catalogReady,
                schedulesResumed,
                lastProgress?.GetString("StopReason"),
                progress.Select(x => x.GetNullableInt64("Cursor")).LastOrDefault(x => x.HasValue),
                progress.Sum(x => x.GetInt32("AdmissionRetries")),
                sends.Length,
                sends.Count(x => !x.Succeeded),
                commandRecords.Length,
                commandRecords.Count(x => !x.Succeeded),
                maxSql,
                runtime.Database.TransactionsStarted - transactionCountStart,
                Percentile(txMs, .50),
                Percentile(txMs, .95),
                txMs.DefaultIfEmpty(0).Max(),
                runtime.Headroom.MinimumLeaseHeadroomMilliseconds,
                runtime.Headroom.MaximumRenewalIntervalMilliseconds,
                runtime.Headroom.LeaseDurationMilliseconds,
                clock.YieldIntervalsMilliseconds,
                progress,
                runtime.Logs.Snapshot(),
                rss,
                cancellationTimestamp,
                completionMs,
                releaseMs,
                cancelledAtPage ? callsAtCancellation : null);
        }
        catch
        {
            await StopJobAsync(job);
            throw;
        }
    }

    private static async Task StopJobAsync(IgdbImportJob job)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await job.StopAsync(stop.Token);
    }

    private static JobProfileSample BuildSample(string name, string mode, JobRunEvidence run,
        FixtureState before, FixtureState after, long databaseBefore, long databaseAfter, long fixtureAgeMilliseconds) =>
        new(name, mode, StagingRows, before.EligibleStagingRows, after.StagingRows,
            run.ElapsedMilliseconds, run.ServiceScopes, run.SessionHttpAttempts, run.SessionAdmissionRows,
            run.AdmittedRows, run.AdmissionRemaining, run.CatalogReady, run.SchedulesResumed,
            run.StopReason, run.Cursor, run.AdmissionRetryCount, run.FakeHttpAttempts,
            run.FakeHttpFailures, run.SqlCommandCount, run.SqlFailures, run.MaximumSqlMilliseconds,
            run.TransactionCount, run.TransactionP50Milliseconds, run.TransactionP95Milliseconds,
            run.TransactionMaxMilliseconds, run.MinimumLeaseHeadroomMilliseconds,
            run.MaximumRenewalIntervalMilliseconds, run.LeaseDurationMilliseconds,
            run.Resources.WorkingSetBytes, run.Resources.PeakWorkingSetBytes, run.Resources.AllocatedBytes,
            databaseBefore, databaseAfter, Math.Max(0, databaseAfter - databaseBefore),
            fixtureAgeMilliseconds, null, run.YieldIntervalsMilliseconds, run.Progress, run.LogEvents);

    private async Task SeedCompletedScanBacklogAsync(string connectionString, int count, CancellationToken ct)
    {
        await ClearProfileRowsAsync(connectionString, ct);
        var fetchedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var releaseAt = DateTimeOffset.UtcNow.AddDays(-2);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        for (var offset = 0; offset < count; offset += UnitAdmissionRows)
        {
            var batch = Math.Min(UnitAdmissionRows, count - offset);
            var sql = new StringBuilder("INSERT INTO igdb_imports (igdb_game_id, name, first_release_date, game_type_name, cover_image_id, provider_updated_at, fetched_at, created_at, updated_at) VALUES ");
            await using var command = new NpgsqlCommand { Connection = connection, CommandTimeout = StatementSeconds };
            command.Parameters.AddWithValue("releaseAt", releaseAt);
            command.Parameters.AddWithValue("fetchedAt", fetchedAt);
            for (var index = 0; index < batch; index++)
            {
                if (index > 0) sql.Append(',');
                var id = offset + index + 1L;
                sql.Append($"(@id{index}, @name{index}, @releaseAt, 'Main game', @cover{index}, @fetchedAt, @fetchedAt, @fetchedAt, @fetchedAt)");
                command.Parameters.AddWithValue($"id{index}", id);
                command.Parameters.AddWithValue($"name{index}", $"Profile fixture game {id}");
                command.Parameters.AddWithValue($"cover{index}", $"profile-cover-{id}");
            }
            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(ct);
        }

        await using var state = new NpgsqlCommand(
            "INSERT INTO igdb_import_state (id, bootstrap_completed, run_is_bootstrap, last_committed_id, version) VALUES (1, TRUE, FALSE, 0, 0)", connection)
        { CommandTimeout = StatementSeconds };
        await state.ExecuteNonQueryAsync(ct);
    }

    private async Task SeedEmptyImportStateAsync(string connectionString, bool bootstrapCompleted, CancellationToken ct)
    {
        await ClearProfileRowsAsync(connectionString, ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "INSERT INTO igdb_import_state (id, bootstrap_completed, run_is_bootstrap, last_committed_id, version) VALUES (1, @completed, FALSE, 0, 0)", connection)
        { CommandTimeout = StatementSeconds };
        command.Parameters.AddWithValue("completed", bootstrapCompleted);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ClearProfileRowsAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "DELETE FROM review_fields; DELETE FROM reviews; DELETE FROM media; DELETE FROM media_collections; DELETE FROM media_covers; DELETE FROM igdb_imports; DELETE FROM igdb_import_state;",
            connection) { CommandTimeout = StatementSeconds };
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<string> FixtureConnectionStringAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        return db.Database.GetConnectionString() ?? throw new InvalidOperationException("The fixture database connection string is missing.");
    }

    private static async Task<FixtureState> ReadFixtureStateAsync(string connectionString, CancellationToken ct)
    {
        await using var db = CreateUninstrumentedDb(connectionString);
        var staged = await db.Set<IgdbImport>().AsNoTracking().CountAsync(ct);
        var eligible = await db.Set<IgdbImport>().AsNoTracking().CountAsync(x => x.Name != null && x.Name != ""
            && x.FirstReleaseDate != null && x.FirstReleaseDate < DateTimeOffset.UtcNow.AddDays(1)
            && x.VersionParentId == null && (x.GameTypeName == "Main game" || x.GameTypeName == "Remake" || x.GameTypeName == "Remaster"), ct);
        var media = db.Media.AsNoTracking().Where(x => x.ExternalSource == MediaExternalSource.Igdb);
        var count = await media.CountAsync(ct);
        var distinct = await media.Select(x => x.ExternalId).Distinct().CountAsync(ct);
        var sampleIds = await media.Where(x => x.ExternalId == "1" || x.ExternalId == "2500" || x.ExternalId == "5000")
            .ToDictionaryAsync(x => x.ExternalId!, x => x.Id, ct);
        var state = await db.Set<IgdbImportState>().AsNoTracking().SingleOrDefaultAsync(ct);
        return new(staged, eligible, count, distinct, sampleIds, state?.LastCommittedId ?? 0,
            state?.ClaimedUntil, state?.BootstrapCompleted ?? false);
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadSampleMediaIdentityAsync(string connectionString, CancellationToken ct)
    {
        await using var db = CreateUninstrumentedDb(connectionString);
        return await db.Media.AsNoTracking().Where(x => x.ExternalSource == MediaExternalSource.Igdb
                && (x.ExternalId == "1" || x.ExternalId == "2500" || x.ExternalId == "5000"))
            .ToDictionaryAsync(x => x.ExternalId!, x => x.Id, ct);
    }

    private static PostgreSQLContext CreateUninstrumentedDb(string connectionString) =>
        new(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseNpgsql(connectionString, options => options.CommandTimeout(StatementSeconds))
            .UseSnakeCaseNamingConvention().Options);

    private static async Task<long> ReadDatabaseSizeBytesAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_database_size(current_database())", connection)
        { CommandTimeout = StatementSeconds };
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private static async Task<long> ReadOldestEligibleAgeAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "SELECT MIN(fetched_at) FROM igdb_imports WHERE name IS NOT NULL AND name <> '' AND first_release_date < CURRENT_DATE + INTERVAL '1 day' AND version_parent_id IS NULL AND game_type_name IN ('Main game', 'Remake', 'Remaster')",
            connection) { CommandTimeout = StatementSeconds };
        var value = await command.ExecuteScalarAsync(ct);
        var fetchedAt = value switch
        {
            DateTime date => new DateTimeOffset(date),
            DateTimeOffset date => date,
            _ => throw new InvalidOperationException("The eligible fixture has no fetch timestamp.")
        };
        return Math.Max(0, (long)(DateTimeOffset.UtcNow - fetchedAt).TotalMilliseconds);
    }

    private static async Task WriteArtifactAsync(string outputPath, JobProfileArtifact artifact)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private static IReadOnlyDictionary<string, object> BuildLimits() => new Dictionary<string, object>
    {
        ["stagingRows"] = StagingRows,
        ["eligibility"] = "all 50,000 rows are released, main-game staging rows",
        ["bootstrapSessionAdmissionRows"] = SessionAdmissionRows,
        ["workUnitAdmissionRows"] = UnitAdmissionRows,
        ["workUnitAdmissionBatches"] = 1,
        ["workUnitPages"] = 1,
        ["workUnitHttpAttempts"] = 20,
        ["sessionHttpAttempts"] = 100,
        ["unitSeconds"] = 60,
        ["sessionSeconds"] = 900,
        ["sqlStatementSeconds"] = StatementSeconds,
        ["igdbLeaseSeconds"] = 120,
        ["betweenUnitYieldMilliseconds"] = 1000,
        ["requestsPerSecond"] = 4,
        ["maxConcurrentProviderRequests"] = 1,
        ["maximumProcessPeakWorkingSetBytes"] = MaxRssBytes,
        ["maximumFixtureDatabaseBytes"] = MaxDatabaseBytes
    };

    private static CatalogBootstrapOptions BootstrapOptions() => new()
    {
        Provider = "igdb",
        MaxHttpAttempts = 100,
        MaxAdmissionRows = SessionAdmissionRows,
        MaxSeconds = 900,
        IgdbYieldMilliseconds = 1000
    };

    private static IgdbOptions IgdbProfileOptions() => new()
    {
        ImportEnabled = true,
        ArtworkEnabled = false,
        ClientId = "fixture-client",
        ClientSecret = "fixture-secret",
        RequestsPerSecond = 4,
        MaxConcurrentRequests = 1,
        TimeoutSeconds = 10,
        MaxStatementSeconds = StatementSeconds,
        PageSize = 100,
        WorkUnitPageLimit = 1,
        WorkUnitHttpAttemptLimit = 20,
        WorkUnitAdmissionRowLimit = UnitAdmissionRows,
        WorkUnitAdmissionBatchLimit = 1,
        WorkUnitSeconds = 60,
        LeaseSeconds = 120
    };

    private static void AssertResourceEnvelope(IEnumerable<JobProfileSample> samples,
        IEnumerable<DrainSessionSample> drain, IEnumerable<MixedWorkloadSample> mixed,
        CancellationSample? cancellation)
    {
        var peak = samples.Select(x => x.PeakWorkingSetBytes)
            .Concat(mixed.Select(x => x.PeakWorkingSetBytes))
            .Append(cancellation?.PeakWorkingSetBytes ?? 0)
            .Max();
        peak.Should().BeLessThanOrEqualTo(MaxRssBytes);
        samples.Concat(drain.Select(x => x.AsProfileSample())).Select(x => x.MaximumSqlMilliseconds)
            .Concat(mixed.Select(x => x.MaximumSqlMilliseconds))
            .Append(cancellation?.MaximumSqlMilliseconds ?? 0)
            .Should().OnlyContain(x => x <= StatementSeconds * 1_000);
    }

    private static long? Percentile(IReadOnlyList<long> values, double percentile)
    {
        if (values.Count == 0) return null;
        var ordered = values.OrderBy(x => x).ToArray();
        var index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static long MinimumProviderSpacingMilliseconds(IReadOnlyList<FakeHttpAttempt> attempts)
    {
        var api = attempts.OrderBy(x => x.StartedAt).ToArray();
        return api.Length < 2 ? 0 : api.Zip(api.Skip(1), (left, right) => (long)(right.StartedAt - left.StartedAt).TotalMilliseconds).Min();
    }

    private static ResourceSnapshot CaptureResources()
    {
        using var process = Process.GetCurrentProcess();
        var peak = process.PeakWorkingSet64;
        peak.Should().BeLessThanOrEqualTo(MaxRssBytes, "the profile has a 2 GiB process RSS ceiling");
        return new(process.WorkingSet64, peak, GC.GetTotalAllocatedBytes(precise: false));
    }

    private sealed class ProfileRuntime : IAsyncDisposable
    {
        private readonly HttpClient apiClient;
        private readonly HttpClient twitchClient;
        public ProfileRuntime(string connectionString, bool holdFirstImportPage = false)
        {
            Options = IgdbProfileOptions();
            Http = new ProfileFakeProviderHandler(holdFirstImportPage);
            apiClient = new HttpClient(Http, disposeHandler: false) { BaseAddress = new Uri("http://fixture.invalid/v4/") };
            twitchClient = new HttpClient(Http, disposeHandler: false) { BaseAddress = new Uri("http://fixture.invalid/") };
            Client = new IgdbClient(apiClient, new BulkCatalogHttpClientFactory(twitchClient),
                new IgdbRequestLimiter(Options), Microsoft.Extensions.Options.Options.Create(Options));
            Database = new BulkCatalogDbInstrumentation();
            Headroom = new LeaseHeadroomMetrics();
            ReleaseObserver = new LeaseReleaseObserver();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<PostgreSQLContext>(options => options
                .UseNpgsql(connectionString, settings => settings.CommandTimeout(StatementSeconds))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(Database, Database.TransactionInstrumentation, ReleaseObserver));
            services.AddScoped<IgdbImportSqlProvider>();
            services.AddScoped<IIgdbImportProvider>(sp => new ProfileObservedImportProvider(
                sp.GetRequiredService<IgdbImportSqlProvider>(), Headroom));
            services.AddScoped(sp => new IgdbImportService(Client,
                sp.GetRequiredService<IIgdbImportProvider>(),
                Microsoft.Extensions.Options.Options.Create(Options),
                Microsoft.Extensions.Options.Options.Create(new ArtworkOptions { PositiveCacheDays = 30, NegativeCacheDays = 7 }),
                NullLogger<IgdbImportService>.Instance,
                sp.GetRequiredService<IServiceScopeFactory>()));
            Root = services.BuildServiceProvider(validateScopes: true);
            Logs = new JobProfileLogCapture();
        }

        public IgdbOptions Options { get; }
        public IgdbClient Client { get; }
        public ProfileFakeProviderHandler Http { get; }
        public BulkCatalogDbInstrumentation Database { get; }
        public LeaseHeadroomMetrics Headroom { get; }
        public LeaseReleaseObserver ReleaseObserver { get; }
        public JobProfileLogCapture Logs { get; }
        public ServiceProvider Root { get; }

        public ValueTask DisposeAsync()
        {
            apiClient.Dispose();
            twitchClient.Dispose();
            Http.Dispose();
            return Root.DisposeAsync();
        }
    }

    private sealed class ProfileObservedImportProvider(IIgdbImportProvider inner, LeaseHeadroomMetrics metrics) : IIgdbImportProvider
    {
        public async Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct)
        {
            var lease = await inner.TryAcquireLeaseAsync(now, leaseDuration, ct);
            metrics.Observe(lease);
            return lease;
        }
        public async Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? maximumId,
            DateTimeOffset? updatedAfter, DateTimeOffset? updatedBefore, DateTimeOffset now, CancellationToken ct)
        { var result = await inner.StartRunAsync(lease, bootstrap, maximumId, updatedAfter, updatedBefore, now, ct); metrics.Observe(lease); return result; }
        public async Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page,
            long committedId, CancellationToken ct)
        { var result = await inner.CommitPageAsync(lease, page, committedId, ct); metrics.Observe(lease); return result; }
        public async Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct)
        { await inner.CompleteRunAsync(lease, now, ct); metrics.Observe(lease); }
        public Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct) => inner.ReleaseLeaseAsync(lease, ct);
        public async Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> supportedGameTypes,
            DateTimeOffset now, TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
        { var result = await inner.LoadEligibleGamesAsync(lease, supportedGameTypes, now, positiveCacheDuration, negativeCacheDuration, ct); metrics.Observe(lease); return result; }
        public async Task<IgdbAdmissionResult> LoadEligibleGamesAsync(IgdbImportLease lease,
            IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration,
            TimeSpan negativeCacheDuration, ImportWorkUnitBudget budget, CancellationToken ct)
        { var result = await inner.LoadEligibleGamesAsync(lease, supportedGameTypes, now, positiveCacheDuration, negativeCacheDuration, budget, ct); metrics.Observe(lease); return result; }
        public Task<DateTimeOffset?> GetLeaseBusyUntilAsync(CancellationToken ct) => inner.GetLeaseBusyUntilAsync(ct);
    }

    private sealed class LeaseHeadroomMetrics
    {
        private long minimumHeadroom = long.MaxValue;
        private long maximumRenewalInterval;
        private long lastRenewalTimestamp;
        private DateTimeOffset? lastClaimedUntil;
        private long leaseMilliseconds;
        public long? MinimumLeaseHeadroomMilliseconds => Volatile.Read(ref minimumHeadroom) == long.MaxValue ? null : Volatile.Read(ref minimumHeadroom);
        public long? MaximumRenewalIntervalMilliseconds => Volatile.Read(ref maximumRenewalInterval) <= 0 ? null : Volatile.Read(ref maximumRenewalInterval);
        public long LeaseDurationMilliseconds => Volatile.Read(ref leaseMilliseconds);

        public void Reset()
        {
            Volatile.Write(ref minimumHeadroom, long.MaxValue);
            Volatile.Write(ref maximumRenewalInterval, 0);
            Volatile.Write(ref lastRenewalTimestamp, 0);
            lastClaimedUntil = null;
            Volatile.Write(ref leaseMilliseconds, 0);
        }

        public void Observe(IgdbImportLease? lease)
        {
            if (lease?.State.ClaimedUntil is not { } until) return;
            var observedAt = Stopwatch.GetTimestamp();
            Volatile.Write(ref leaseMilliseconds, (long)lease.LeaseDuration.TotalMilliseconds);
            if (lastClaimedUntil is { } previous && previous != until)
            {
                var interval = (long)Stopwatch.GetElapsedTime(lastRenewalTimestamp, observedAt).TotalMilliseconds;
                InterlockedExtensions.Max(ref maximumRenewalInterval, interval);
                Volatile.Write(ref lastRenewalTimestamp, observedAt);
                lastClaimedUntil = until;
            }
            else if (lastClaimedUntil is null)
            {
                Volatile.Write(ref lastRenewalTimestamp, observedAt);
                lastClaimedUntil = until;
            }
            InterlockedExtensions.Min(ref minimumHeadroom, (long)(until - DateTimeOffset.UtcNow).TotalMilliseconds);
        }
    }

    private sealed class LeaseReleaseObserver : DbCommandInterceptor
    {
        private long lastReleaseTicks;
        public DateTimeOffset? LastReleaseCompletedUtc => Interlocked.Read(ref lastReleaseTicks) is var ticks && ticks != 0
            ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        { Observe(command); return result; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        { Observe(command); return ValueTask.FromResult(result); }
        private void Observe(DbCommand command)
        {
            var sql = command.CommandText;
            if (sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && sql.Contains("igdb_import_state", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(sql, @"\bclaim_token\s*=\s*NULL\b", RegexOptions.IgnoreCase)
                && Regex.IsMatch(sql, @"\bclaimed_until\s*=\s*NULL\b", RegexOptions.IgnoreCase))
                Interlocked.Exchange(ref lastReleaseTicks, DateTimeOffset.UtcNow.UtcTicks);
        }
    }

    private sealed class ProfileFakeProviderHandler(bool holdFirstImportPage) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<FakeHttpAttempt> attempts = new();
        private readonly TaskCompletionSource<bool> releaseFirstImportPage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        private int active;
        private int maxConcurrency;
        private int heldImportPage;
        public IReadOnlyList<FakeHttpAttempt> Attempts => attempts.ToArray();
        public int TotalCalls => Volatile.Read(ref calls);
        public int MaxConcurrency => Volatile.Read(ref maxConcurrency);
        public TaskCompletionSource<bool> FirstImportPageStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            var activeNow = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref maxConcurrency, activeNow);
            var started = DateTimeOffset.UtcNow;
            var path = request.RequestUri?.AbsolutePath ?? "/";
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var operation = Classify(path, body);
            var status = HttpStatusCode.OK;
            var succeeded = false;
            try
            {
                if (holdFirstImportPage && operation == "games" && body.Contains("id > 0", StringComparison.Ordinal)
                    && Interlocked.Exchange(ref heldImportPage, 1) == 0)
                {
                    FirstImportPageStarted.TrySetResult(true);
                    await releaseFirstImportPage.Task.WaitAsync(cancellationToken);
                }
                await Task.Yield();
                var content = operation switch
                {
                    "token" => "{\"access_token\":\"fixture-token\",\"expires_in\":3600}",
                    "game_types" => "[{\"id\":0,\"type\":\"Main game\"},{\"id\":8,\"type\":\"Remake\"},{\"id\":9,\"type\":\"Remaster\"}]",
                    "maximum_id" => "[{\"id\":201}]",
                    "artwork" => ArtworkResponse(body),
                    "games" => GamePage(body),
                    _ => throw new InvalidOperationException("Unexpected fake provider operation.")
                };
                var response = new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
                succeeded = true;
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                status = (HttpStatusCode)599;
                throw;
            }
            catch
            {
                status = (HttpStatusCode)599;
                throw;
            }
            finally
            {
                attempts.Enqueue(new FakeHttpAttempt(started, DateTimeOffset.UtcNow, operation, request.Method.Method,
                    path, (int)status, succeeded));
                Interlocked.Decrement(ref active);
            }
        }

        private static string Classify(string path, string body)
        {
            if (path.EndsWith("/oauth2/token", StringComparison.OrdinalIgnoreCase)) return "token";
            if (path.EndsWith("/game_types", StringComparison.OrdinalIgnoreCase)) return "game_types";
            if (body.Contains("fields id,cover.image_id", StringComparison.Ordinal)) return "artwork";
            if (body.Contains("sort id desc", StringComparison.Ordinal)) return "maximum_id";
            if (path.EndsWith("/games", StringComparison.OrdinalIgnoreCase)) return "games";
            return "unknown";
        }

        private static string ArtworkResponse(string body)
        {
            var match = Regex.Match(body, @"where id = (\d+)");
            if (!match.Success) throw new InvalidOperationException("Malformed fixture artwork query.");
            var id = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (id is < 101 or > 103) throw new InvalidOperationException("Unexpected fixture artwork identity.");
            return $"[{{\"id\":{id},\"cover\":{{\"image_id\":\"profile-cover-{id}\"}}}}]";
        }

        private static string GamePage(string body)
        {
            var match = Regex.Match(body, @"where id > (\d+)");
            var after = match.Success ? long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var count = (int)Math.Min(100, Math.Max(0, 201 - after));
            var games = Enumerable.Range(1, count).Select(offset =>
            {
                var id = after + offset;
                return new { id, name = $"HTTP profile game {id}", game_type = 0,
                    first_release_date = 1_700_000_000, updated_at = 1_700_000_000,
                    cover = new { image_id = $"profile-cover-{id}" } };
            });
            return JsonSerializer.Serialize(games);
        }
    }

    private sealed class JobProfileLogCapture
    {
        private readonly object gate = new();
        private readonly List<JobProfileLogEvent> events = [];
        private readonly SemaphoreSlim progressSignal = new(0);
        public ILogger<T> CreateLogger<T>() => new JobProfileLogger<T>(this);
        public IReadOnlyList<JobProfileLogEvent> Snapshot() { lock (gate) return events.ToArray(); }
        public IReadOnlyList<JobProfileProgress> ProgressSnapshot() => Snapshot().Where(x => x.Kind == "progress").Select(x => x.Progress!).ToArray();
        public JobProfileProgress? SessionTotalsSnapshot() => Snapshot().LastOrDefault(x => x.Kind == "totals")?.Progress;
        public async Task<JobProfileProgress> WaitForProgressAsync(int count, CancellationToken ct)
        {
            while (true)
            {
                lock (gate)
                    if (events.Count(x => x.Kind == "progress") >= count)
                        return events.Where(x => x.Kind == "progress").ElementAt(count - 1).Progress!;
                await progressSignal.WaitAsync(ct);
            }
        }
        public void Reset() { lock (gate) events.Clear(); while (progressSignal.Wait(0)) { } }
        private void Add(LogLevel level, string template, string message, IReadOnlyDictionary<string, object?> values)
        {
            var kind = template.StartsWith("IGDB {Mode} {SessionId}:", StringComparison.Ordinal) ? "progress"
            : template.Contains(" totals:", StringComparison.Ordinal) ? "totals" : "other";
            var selected = values.Where(x => ProfileLogKeys.Contains(x.Key))
                .ToDictionary(x => x.Key, x => FormatValue(x.Value), StringComparer.Ordinal);
            var progress = kind is "progress" or "totals"
                ? new JobProfileProgress(DateTimeOffset.UtcNow, template, selected) : null;
            lock (gate) events.Add(new JobProfileLogEvent(DateTimeOffset.UtcNow, level.ToString(), kind, template, message, progress));
            if (kind == "progress") progressSignal.Release();
        }
        private static string? FormatValue(object? value) => value switch
        {
            null => null,
            IReadOnlyDictionary<string, int> dictionary => JsonSerializer.Serialize(dictionary),
            IEnumerable<KeyValuePair<string, int>> pairs => JsonSerializer.Serialize(pairs.ToDictionary(x => x.Key, x => x.Value)),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        private static readonly HashSet<string> ProfileLogKeys = new(StringComparer.Ordinal)
        {
            "Mode", "SessionId", "HttpLimit", "RowLimit", "Seconds", "StopReason", "LeaseAcquired",
            "Pages", "Rows", "Admitted", "UpstreamComplete", "AdmissionRemaining", "CatalogReady",
            "IncrementalPending", "LeaseBusyUntil", "RetryAfter", "Cursor", "MaximumId", "AdmissionRetries",
            "FailingBatch", "HttpAttempts", "AdmissionRows", "ElapsedMs", "@HttpOperations"
        };

        private sealed class JobProfileLogger<T>(JobProfileLogCapture owner) : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? pairs.Where(x => x.Key != "{OriginalFormat}").ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
                    : new Dictionary<string, object?>();
                var template = state is IEnumerable<KeyValuePair<string, object?>> all
                    ? all.FirstOrDefault(x => x.Key == "{OriginalFormat}").Value?.ToString() ?? formatter(state, exception)
                    : formatter(state, exception);
                owner.Add(logLevel, template, formatter(state, exception), values);
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();
            public void Dispose() { }
        }
    }

    private sealed class YieldRecordingTimeProvider : TimeProvider
    {
        private readonly ConcurrentQueue<long> yieldIntervals = new();
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override TimeZoneInfo LocalTimeZone => TimeProvider.System.LocalTimeZone;
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == TimeSpan.FromSeconds(1)) yieldIntervals.Enqueue((long)dueTime.TotalMilliseconds);
            return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
        }
        public IReadOnlyList<long> YieldIntervalsMilliseconds => yieldIntervals.ToArray();
    }

    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public IServiceScope CreateScope() { Interlocked.Increment(ref count); return inner.CreateScope(); }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            do { current = Volatile.Read(ref target); if (value <= current) return; }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
        public static void Max(ref long target, long value)
        {
            long current;
            do { current = Volatile.Read(ref target); if (value <= current) return; }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
        public static void Min(ref long target, long value)
        {
            long current;
            do { current = Volatile.Read(ref target); if (value >= current) return; }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }

    private sealed record JobRunEvidence(
        long ElapsedMilliseconds,
        int ServiceScopes,
        int SessionHttpAttempts,
        int SessionAdmissionRows,
        int AdmittedRows,
        int? AdmissionRemaining,
        bool CatalogReady,
        bool SchedulesResumed,
        string? StopReason,
        long? Cursor,
        int AdmissionRetryCount,
        int FakeHttpAttempts,
        int FakeHttpFailures,
        int SqlCommandCount,
        int SqlFailures,
        long MaximumSqlMilliseconds,
        int TransactionCount,
        long? TransactionP50Milliseconds,
        long? TransactionP95Milliseconds,
        long TransactionMaxMilliseconds,
        long? MinimumLeaseHeadroomMilliseconds,
        long? MaximumRenewalIntervalMilliseconds,
        long LeaseDurationMilliseconds,
        IReadOnlyList<long> YieldIntervalsMilliseconds,
        IReadOnlyList<JobProfileProgress> Progress,
        IReadOnlyList<JobProfileLogEvent> LogEvents,
        ResourceSnapshot Resources,
        DateTimeOffset? CancellationTimestampUtc,
        long? CancellationToJobCompletionMilliseconds,
        long? CancellationToLeaseReleaseMilliseconds,
        int? ProviderCallsAtCancellation);

    private sealed record FixtureState(int StagingRows, int EligibleStagingRows, int MediaCount,
        int DistinctIgdbExternalIds, IReadOnlyDictionary<string, long> MediaIdsByExternalId,
        long LastCommittedId, DateTimeOffset? ClaimedUntil, bool BootstrapCompleted);
    private sealed record ResourceSnapshot(long WorkingSetBytes, long PeakWorkingSetBytes, long AllocatedBytes);
    private sealed record FakeHttpAttempt(DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Operation,
        string Method, string Path, int StatusCode, bool Succeeded);
    private sealed record JobProfileProgress(DateTimeOffset TimestampUtc, string Template, IReadOnlyDictionary<string, string?> Values)
    {
        public string? GetString(string key) => Values.GetValueOrDefault(key);
        public int GetInt32(string key) => int.Parse(Values[key]!, CultureInfo.InvariantCulture);
        public int? GetNullableInt32(string key) => Values.TryGetValue(key, out var value) && value is not null
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        public long? GetNullableInt64(string key) => Values.TryGetValue(key, out var value) && value is not null
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        public bool GetBoolean(string key) => bool.TryParse(Values.GetValueOrDefault(key), out var parsed) && parsed;
    }
    private sealed record JobProfileLogEvent(DateTimeOffset TimestampUtc, string Level, string Kind,
        string Template, string Message, JobProfileProgress? Progress);
    private sealed record JobProfileSample(string Name, string Mode, int StagingRows, int EligibleRows,
        int StagingRowsAfter, long ElapsedMilliseconds, int FreshServiceScopes, int ImportHttpAttempts,
        int AdmissionRowsReserved, int AdmissionRowsLoaded, int? AdmissionRemaining, bool CatalogReady,
        bool SchedulesResumed, string? StopReason, long? DurableCursor, int AdmissionRetryCount,
        int AggregateProviderHttpAttempts, int ProviderHttpFailures, int SqlCommands, int SqlFailures,
        long MaximumSqlMilliseconds, int TransactionsStarted, long? TransactionP50Milliseconds,
        long? TransactionP95Milliseconds, long TransactionMaximumMilliseconds,
        long? MinimumLeaseHeadroomMilliseconds, long? MaximumRenewalIntervalMilliseconds,
        long LeaseDurationMilliseconds, long WorkingSetBytes, long PeakWorkingSetBytes, long AllocatedBytes,
        long DatabaseBytesBefore, long DatabaseBytesAfter, long DatabaseBytesDelta,
        long OldestEligibleFetchedAgeMilliseconds, long? TemporaryDownloadDiskBytes,
        IReadOnlyList<long> YieldIntervalsMilliseconds, IReadOnlyList<JobProfileProgress> Progress,
        IReadOnlyList<JobProfileLogEvent> ProgressLogs);
    private sealed record DrainSessionSample(int Session, long ElapsedMilliseconds, int FreshServiceScopes,
        int AdmissionRowsLoaded, int? AdmissionRemaining, bool CatalogReady, int MediaCount,
        int DistinctIgdbExternalIds, int SqlCommands, long? TransactionP50Milliseconds,
        long? TransactionP95Milliseconds, long TransactionMaximumMilliseconds,
        IReadOnlyList<long> YieldIntervalsMilliseconds, IReadOnlyList<JobProfileProgress> Progress)
    {
        public JobProfileSample AsProfileSample() => new($"drain-{Session}", "backlog-drain", StagingRows,
            StagingRows, StagingRows, ElapsedMilliseconds, FreshServiceScopes, 0, AdmissionRowsLoaded,
            AdmissionRowsLoaded, AdmissionRemaining, CatalogReady, CatalogReady, null, null, 0, 0, 0,
            SqlCommands, 0, 0, 0, TransactionP50Milliseconds, TransactionP95Milliseconds,
            TransactionMaximumMilliseconds, null, null, 0, 0, 0, 0, 0, 0, 0, 0, null,
            YieldIntervalsMilliseconds, Progress, []);
    }
    private sealed record MixedWorkloadSample(long ElapsedMilliseconds, int FreshServiceScopes, int ImportedMedia,
        int ImportHttpAttempts, int AttributedImportSends, int ArtworkSends, int AggregateHttpAttempts,
        int HttpFailures, int MaxHttpConcurrency, long MinimumProviderSpacingMilliseconds, int AdmittedRows,
        int? AdmissionRemaining, bool CatalogReady, bool SchedulesResumed, int SqlCommands, int SqlFailures,
        long? TransactionP50Milliseconds, long? TransactionP95Milliseconds, long TransactionMaximumMilliseconds,
        long? MinimumLeaseHeadroomMilliseconds, long? MaximumRenewalIntervalMilliseconds,
        IReadOnlyList<long> YieldIntervalsMilliseconds, IReadOnlyList<JobProfileProgress> Progress,
        IReadOnlyList<FakeHttpAttempt> HttpTimeline)
    {
        public long PeakWorkingSetBytes { get; init; }
        public long MaximumSqlMilliseconds { get; init; }
    }
    private sealed record CancellationSample(long ElapsedMilliseconds, long? CancellationToJobCompletionMilliseconds,
        long? CancellationToLeaseReleaseMilliseconds, int ProviderCallsAtCancellation, int AggregateHttpAttempts,
        int ImportHttpAttempts, long? DurableCursor, DateTimeOffset? ClaimedUntil, bool SchedulesResumed,
        int SqlCommands, int SqlFailures, long? TransactionP50Milliseconds, long? TransactionP95Milliseconds,
        long TransactionMaximumMilliseconds, IReadOnlyList<JobProfileProgress> Progress,
        IReadOnlyList<FakeHttpAttempt> HttpTimeline)
    {
        public long PeakWorkingSetBytes { get; init; }
        public long MaximumSqlMilliseconds { get; init; }
    }
    private sealed record JobProfileArtifact(DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Arm,
        string Isolation, string Command, IReadOnlyDictionary<string, object> EffectiveLimits,
        long DatabaseBytesAtStart, long DatabaseBytesAtFinish, IReadOnlyList<JobProfileSample> BacklogSamples,
        IReadOnlyList<DrainSessionSample> DrainSessions, IReadOnlyList<MixedWorkloadSample> MixedWorkloadSamples,
        CancellationSample? Cancellation, IReadOnlyList<string> CoverageGaps);
}
