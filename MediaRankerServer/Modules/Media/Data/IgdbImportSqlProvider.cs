using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MediaRankerServer.Modules.Media.Data;

public sealed class IgdbImportSqlProvider(
    PostgreSQLContext dbContext,
    ILogger<IgdbImportSqlProvider> logger,
    IServiceScopeFactory? scopeFactory = null,
    IOptions<IgdbOptions>? igdbOptions = null) : IIgdbImportProvider
{
    private readonly int maxStatementSeconds = Math.Max(1, igdbOptions?.Value.MaxStatementSeconds ?? 15);

    public async Task<DateTimeOffset?> GetLeaseBusyUntilAsync(CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        return await dbContext.Set<IgdbImportState>().AsNoTracking()
            .Where(x => x.Id == IgdbImportState.SingletonId && x.ClaimedUntil != null)
            .Select(x => x.ClaimedUntil)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        await dbContext.Database.ExecuteSqlRawAsync("INSERT INTO igdb_import_state (id, bootstrap_completed, run_is_bootstrap, last_committed_id, version) VALUES (1, FALSE, FALSE, 0, 0) ON CONFLICT (id) DO NOTHING", ct);

        var token = Guid.NewGuid();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var state = await dbContext.Set<IgdbImportState>().FromSqlInterpolated($"""
            SELECT * FROM igdb_import_state
            WHERE id = {IgdbImportState.SingletonId}
            FOR UPDATE
            """).SingleAsync(ct);
        var databaseNow = await GetDatabaseNowAsync(ct);
        if (state.ClaimedUntil is { } claimedUntil && claimedUntil > databaseNow)
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }

        var acquired = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE igdb_import_state
            SET claim_token = {token},
                claimed_until = clock_timestamp() + {leaseDuration},
                version = version + 1
            WHERE id = {IgdbImportState.SingletonId}
              AND version = {state.Version}
            """, ct);
        if (acquired != 1)
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }

        await transaction.CommitAsync(ct);

        dbContext.ChangeTracker.Clear();
        state = await dbContext.Set<IgdbImportState>().AsNoTracking()
            .SingleAsync(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == token, ct);
        return new IgdbImportLease(state, token, leaseDuration);
    }

    public async Task<IgdbImportState> StartRunAsync(
        IgdbImportLease lease,
        bool bootstrap,
        long? maximumId,
        DateTimeOffset? updatedAfter,
        DateTimeOffset? updatedBefore,
        DateTimeOffset now,
        CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var ownedState = await dbContext.Set<IgdbImportState>().FromSqlInterpolated($"""
            SELECT * FROM igdb_import_state
            WHERE id = {IgdbImportState.SingletonId} AND claim_token = {lease.Token}
            FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (ownedState is null || ownedState.Version != lease.State.Version
            || ownedState.ClaimedUntil <= await GetDatabaseNowAsync(ct))
            throw new InvalidOperationException("IGDB import lease was lost before the run could start.");

        var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE igdb_import_state
            SET run_is_bootstrap = {bootstrap},
                run_maximum_id = {maximumId},
                last_committed_id = 0,
                run_updated_after = {updatedAfter},
                run_updated_before = {updatedBefore},
                bootstrap_started_at = CASE WHEN {bootstrap} AND bootstrap_started_at IS NULL
                    THEN clock_timestamp() ELSE bootstrap_started_at END,
                claimed_until = clock_timestamp() + {lease.LeaseDuration},
                version = version + 1
            WHERE id = {IgdbImportState.SingletonId}
              AND claim_token = {lease.Token}
              AND claimed_until > clock_timestamp()
              AND version = {lease.State.Version}
            """, ct);
        if (updated != 1)
            throw new InvalidOperationException("IGDB import lease was lost before the run could start.");

        await transaction.CommitAsync(ct);
        dbContext.ChangeTracker.Clear();
        lease.State = await GetOwnedStateAsync(lease.Token, ct);
        return lease.State;
    }

    public async Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long committedId, CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var ownedState = await dbContext.Set<IgdbImportState>().FromSqlInterpolated($"""
                SELECT * FROM igdb_import_state
                WHERE id = {IgdbImportState.SingletonId} AND claim_token = {lease.Token}
                FOR UPDATE
                """).SingleOrDefaultAsync(ct);
            if (ownedState is null || ownedState.Version != lease.State.Version
                || ownedState.ClaimedUntil <= await GetDatabaseNowAsync(ct))
                throw new InvalidOperationException("IGDB import lease was lost before the page could commit.");

            var ids = page.Select(x => x.IgdbGameId).Distinct().ToArray();
            var existing = ids.Length == 0
                ? new Dictionary<long, IgdbImport>()
                : await dbContext.Set<IgdbImport>().Where(x => ids.Contains(x.IgdbGameId)).ToDictionaryAsync(x => x.IgdbGameId, ct);

            foreach (var incoming in page)
            {
                if (!existing.TryGetValue(incoming.IgdbGameId, out var staged))
                {
                    dbContext.Set<IgdbImport>().Add(incoming);
                    continue;
                }

                // The overlap intentionally replays pages. Older provider versions must never replace newer staging.
                if (staged.ProviderUpdatedAt is { } currentVersion
                    && (incoming.ProviderUpdatedAt is null || incoming.ProviderUpdatedAt < currentVersion))
                    continue;

                // A replay is only useful when it was fetched at least as recently. Keep both ordering
                // guards monotonic so a delayed page cannot renew freshness or replace a newer cover.
                if (incoming.FetchedAt < staged.FetchedAt)
                    continue;

                staged.Name = incoming.Name;
                staged.FirstReleaseDate = incoming.FirstReleaseDate;
                staged.GameTypeId = incoming.GameTypeId;
                staged.GameTypeName = incoming.GameTypeName;
                staged.VersionParentId = incoming.VersionParentId;
                staged.CoverImageId = incoming.CoverImageId;
                staged.ProviderUpdatedAt = incoming.ProviderUpdatedAt;
                staged.FetchedAt = incoming.FetchedAt;
            }

            await dbContext.SaveChangesAsync(ct);
            var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE igdb_import_state
                SET last_committed_id = {committedId},
                    claimed_until = clock_timestamp() + {lease.LeaseDuration},
                    version = version + 1
                WHERE id = {IgdbImportState.SingletonId}
                  AND claim_token = {lease.Token}
                  AND claimed_until > clock_timestamp()
                  AND version = {lease.State.Version}
                """, ct);
            if (updated != 1)
                throw new InvalidOperationException("IGDB import lease was lost before the page could commit.");

            await transaction.CommitAsync(ct);
            dbContext.ChangeTracker.Clear();
            lease.State = await GetOwnedStateAsync(lease.Token, ct);
            return lease.State;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError("IGDB staging page transaction failed; category {Category}; count {Count}; first ID {FirstId}.",
                ex.GetType().Name, page.Count, page.Count == 0 ? null : page[0].IgdbGameId);
            throw;
        }
    }

    public async Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        var state = lease.State;
        var completedAt = state.RunIsBootstrap ? (state.BootstrapStartedAt ?? now) : state.RunUpdatedBefore;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var ownedState = await dbContext.Set<IgdbImportState>().FromSqlInterpolated($"""
            SELECT * FROM igdb_import_state
            WHERE id = {IgdbImportState.SingletonId} AND claim_token = {lease.Token}
            FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (ownedState is null || ownedState.Version != state.Version
            || ownedState.ClaimedUntil <= await GetDatabaseNowAsync(ct))
            throw new InvalidOperationException("IGDB import lease was lost before the run could complete.");

        var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE igdb_import_state
            SET bootstrap_completed = {state.RunIsBootstrap} OR bootstrap_completed,
                last_completed_updated_at = {completedAt},
                run_is_bootstrap = FALSE,
                run_maximum_id = NULL,
                last_committed_id = 0,
                run_updated_after = NULL,
                run_updated_before = NULL,
                version = version + 1
            WHERE id = {IgdbImportState.SingletonId}
              AND claim_token = {lease.Token}
              AND claimed_until > clock_timestamp()
              AND version = {state.Version}
            """, ct);
        if (updated != 1)
            throw new InvalidOperationException("IGDB import lease was lost before the run could complete.");

        await transaction.CommitAsync(ct);
        dbContext.ChangeTracker.Clear();
        lease.State = await GetOwnedStateAsync(lease.Token, ct);
    }

    public async Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        await dbContext.Set<IgdbImportState>()
            .Where(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == lease.Token)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ClaimToken, (Guid?)null)
                .SetProperty(x => x.ClaimedUntil, (DateTimeOffset?)null), ct);
    }

    public async Task<int> LoadEligibleGamesAsync(
        IgdbImportLease lease,
        IReadOnlySet<string> supportedGameTypes,
        DateTimeOffset now,
        TimeSpan positiveCacheDuration,
        TimeSpan negativeCacheDuration,
        CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        const int batchSize = 500;
        var supportedTypes = supportedGameTypes.Select(x => x.Trim().ToLowerInvariant()).ToArray();
        var nextUtcDate = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        long lastId = 0;
        var loaded = 0;
        while (true)
        {
            // Keyset pagination keeps memory bounded, including daily release-day admission.
            var eligible = await dbContext.Set<IgdbImport>().AsNoTracking()
                .Where(x => x.IgdbGameId > lastId && x.Name != null && x.Name != ""
                    && x.FirstReleaseDate != null && x.FirstReleaseDate < nextUtcDate && x.VersionParentId == null
                    && x.GameTypeName != null && supportedTypes.Contains(x.GameTypeName.ToLower()))
                .Where(x => !dbContext.Media.Any(m => m.ExternalSource == MediaExternalSource.Igdb
                    && m.ExternalId == x.IgdbGameId.ToString()
                    && m.Cover != null
                    && m.Cover.CheckedAt >= x.FetchedAt
                    && (m.UpdatedAt >= x.FetchedAt
                        || (m.Title == x.Name
                            && m.ReleaseDate == DateOnly.FromDateTime(x.FirstReleaseDate!.Value.UtcDateTime)))))
                .OrderBy(x => x.IgdbGameId).Take(batchSize).ToListAsync(ct);
            if (eligible.Count == 0) return loaded;
            loaded += await LoadEligibleBatchAsync(lease, eligible, positiveCacheDuration, negativeCacheDuration, ct);
            lastId = eligible[^1].IgdbGameId;
            if (eligible.Count < batchSize) return loaded;
        }
    }

    public async Task<IgdbAdmissionResult> LoadEligibleGamesAsync(
        IgdbImportLease lease,
        IReadOnlySet<string> supportedGameTypes,
        DateTimeOffset now,
        TimeSpan positiveCacheDuration,
        TimeSpan negativeCacheDuration,
        ImportWorkUnitBudget budget,
        CancellationToken ct)
    {
        using var commandTimeout = ApplyStatementTimeout();
        var supportedTypes = supportedGameTypes.Select(x => x.Trim().ToLowerInvariant()).ToArray();
        var nextUtcDate = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        long lastId = 0;
        var loaded = 0;
        var batches = 0;
        var retryCount = 0;

        try
        {
            while (true)
            {
                budget.SetStopReason(ImportStopReason.None);
                var remainingRows = budget.RemainingAdmissionRows;
                if (remainingRows <= 0)
                {
                    var reason = budget.Session.AdmissionRows >= budget.Session.Limits.MaxAdmissionRows
                        ? ImportStopReason.SessionAdmissionLimit
                        : ImportStopReason.UnitAdmissionLimit;
                    var remaining = await CountEligibleAsync(lastId, supportedTypes, nextUtcDate, ct);
                    if (remaining == 0)
                        return new IgdbAdmissionResult(loaded, batches, 0, true, false, RetryCount: retryCount);
                    budget.SetStopReason(reason);
                    return new IgdbAdmissionResult(loaded, batches,
                        remaining, false, false, reason.ToString(), RetryCount: retryCount);
                }

                var batchSize = Math.Min(500, remainingRows);
                var eligible = await QueryEligibleAsync(lastId, supportedTypes, nextUtcDate, batchSize, ct);
                if (eligible.Count == 0)
                    return new IgdbAdmissionResult(loaded, batches, 0, true, false, RetryCount: retryCount);

                if (budget.RemainingAdmissionBatches <= 0)
                {
                    var remaining = await CountEligibleAsync(lastId, supportedTypes, nextUtcDate, ct);
                    if (remaining == 0)
                        return new IgdbAdmissionResult(loaded, batches, 0, true, false, RetryCount: retryCount);
                    budget.SetStopReason(ImportStopReason.UnitAdmissionLimit);
                    return new IgdbAdmissionResult(loaded, batches,
                        remaining, false,
                        false, ImportStopReason.UnitAdmissionLimit.ToString(), RetryCount: retryCount);
                }
                if (!budget.TryReserveAdmission(eligible.Count, out var rowReason))
                    return new IgdbAdmissionResult(loaded, batches,
                        await CountEligibleAsync(lastId, supportedTypes, nextUtcDate, ct), false,
                        false, rowReason.ToString(), RetryCount: retryCount);
                if (!budget.TryReserveAdmissionBatch(out var batchReason))
                    return new IgdbAdmissionResult(loaded, batches,
                        await CountEligibleAsync(lastId, supportedTypes, nextUtcDate, ct), false,
                        false, batchReason.ToString(), RetryCount: retryCount);

                try
                {
                    loaded += await LoadEligibleBatchAsync(lease, eligible, positiveCacheDuration, negativeCacheDuration, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (DbUpdateConcurrencyException)
                {
                    var recovery = await RetryInFreshContextAsync(lease, eligible, positiveCacheDuration,
                        negativeCacheDuration, ct);
                    retryCount += recovery.RetryCount;
                    if (recovery.Failed)
                    {
                        return new IgdbAdmissionResult(loaded, batches, null, false, true,
                            "batch_failed", DescribeFailedBatch(eligible, recovery.FailureCategory), retryCount);
                    }
                    loaded += recovery.Loaded;
                }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("IGDB import lease was lost", StringComparison.Ordinal))
                {
                    throw;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    var recovery = await RetryInFreshContextAsync(lease, eligible, positiveCacheDuration,
                        negativeCacheDuration, ct);
                    retryCount += recovery.RetryCount;
                    if (recovery.Failed)
                    {
                        return new IgdbAdmissionResult(loaded, batches, null, false, true,
                            "batch_failed", DescribeFailedBatch(eligible, recovery.FailureCategory), retryCount);
                    }
                    loaded += recovery.Loaded;
                }
                catch (Exception ex)
                {
                    logger.LogError("IGDB admission batch failed; category {Category}; count {Count}; first ID {FirstId}",
                        ex.GetType().Name, eligible.Count, eligible[0].IgdbGameId);
                    return new IgdbAdmissionResult(loaded, batches, null, false, true,
                        "batch_failed", DescribeFailedBatch(eligible, ex.GetType().Name), retryCount);
                }

                batches++;
                lastId = eligible[^1].IgdbGameId;
                if (eligible.Count < batchSize)
                    return new IgdbAdmissionResult(loaded, batches, 0, true, false, RetryCount: retryCount);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("IGDB import lease was lost", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError("IGDB admission eligibility failed; category {Category}; after ID {AfterId}",
                ex.GetType().Name, lastId);
            return new IgdbAdmissionResult(loaded, batches, null, false, true,
                "eligibility_query_failed",
                FormattableString.Invariant($"after={lastId}; count=unknown; category={ex.GetType().Name}"), retryCount);
        }
    }

    private Task<List<IgdbImport>> QueryEligibleAsync(long lastId, string[] supportedTypes,
        DateTimeOffset nextUtcDate, int batchSize, CancellationToken ct) => dbContext.Set<IgdbImport>().AsNoTracking()
        .Where(x => x.IgdbGameId > lastId && x.Name != null && x.Name != ""
            && x.FirstReleaseDate != null && x.FirstReleaseDate < nextUtcDate && x.VersionParentId == null
            && x.GameTypeName != null && supportedTypes.Contains(x.GameTypeName.ToLower()))
        .Where(x => !dbContext.Media.Any(m => m.ExternalSource == MediaExternalSource.Igdb
            && m.ExternalId == x.IgdbGameId.ToString()
            && m.Cover != null
            && m.Cover.CheckedAt >= x.FetchedAt
            && (m.UpdatedAt >= x.FetchedAt
                || (m.Title == x.Name
                    && m.ReleaseDate == DateOnly.FromDateTime(x.FirstReleaseDate!.Value.UtcDateTime)))))
        .OrderBy(x => x.IgdbGameId).Take(batchSize).ToListAsync(ct);

    private static string DescribeFailedBatch(List<IgdbImport> rows, string? category) =>
        FormattableString.Invariant($"{rows[0].IgdbGameId}..{rows[^1].IgdbGameId}; count={rows.Count}; category={category ?? "unknown"}");

    private async Task<(int Loaded, int RetryCount, bool Failed, string? FailureCategory)> RetryInFreshContextAsync(IgdbImportLease lease, List<IgdbImport> eligible,
        TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
    {
        for (var retry = 1; retry <= 2; retry++)
        {
            // A failed attempt leaves its ownership row and possibly inserted entities
            // tracked here. A fresh provider advances lease.State, so discard the stale
            // graph before every retry and before this context is used again.
            dbContext.ChangeTracker.Clear();
            try
            {
                if (scopeFactory is null)
                {
                    return (await LoadEligibleBatchAsync(lease, eligible, positiveCacheDuration, negativeCacheDuration, ct), retry, false, null);
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var freshProvider = scope.ServiceProvider.GetRequiredService<IIgdbImportProvider>() as IgdbImportSqlProvider
                    ?? throw new InvalidOperationException("IGDB import provider registration did not resolve the SQL provider.");
                return (await freshProvider.LoadEligibleBatchAsync(lease, eligible, positiveCacheDuration, negativeCacheDuration, ct), retry, false, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException) when (retry < 2)
            {
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("IGDB import lease was lost", StringComparison.Ordinal))
            {
                throw;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } && retry < 2)
            {
            }
            catch (Exception ex)
            {
                logger.LogError("IGDB admission recovery failed; category {Category}; retry count {RetryCount}; count {Count}; first ID {FirstId}",
                    ex.GetType().Name, retry, eligible.Count, eligible[0].IgdbGameId);
                return (0, retry, true, ex.GetType().Name);
            }
        }

        return (0, 2, true, "retry_exhausted");
    }

    private Task<int> CountEligibleAsync(long lastId, string[] supportedTypes,
        DateTimeOffset nextUtcDate, CancellationToken ct) => dbContext.Set<IgdbImport>().AsNoTracking()
        .Where(x => x.IgdbGameId > lastId && x.Name != null && x.Name != ""
            && x.FirstReleaseDate != null && x.FirstReleaseDate < nextUtcDate && x.VersionParentId == null
            && x.GameTypeName != null && supportedTypes.Contains(x.GameTypeName.ToLower()))
        .Where(x => !dbContext.Media.Any(m => m.ExternalSource == MediaExternalSource.Igdb
            && m.ExternalId == x.IgdbGameId.ToString()
            && m.Cover != null
            && m.Cover.CheckedAt >= x.FetchedAt
            && (m.UpdatedAt >= x.FetchedAt
                || (m.Title == x.Name
                    && m.ReleaseDate == DateOnly.FromDateTime(x.FirstReleaseDate!.Value.UtcDateTime)))))
        .CountAsync(ct);

    private async Task<DateTimeOffset> GetDatabaseNowAsync(CancellationToken ct) =>
        await dbContext.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    private async Task<int> LoadEligibleBatchAsync(IgdbImportLease lease, List<IgdbImport> eligible,
        TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
    {
        // Recovery uses a new provider/context and calls this method directly, so
        // the bound must be applied here as well as around public entry points.
        using var commandTimeout = ApplyStatementTimeout();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var ownedState = await dbContext.Set<IgdbImportState>().FromSqlInterpolated($"""
            SELECT * FROM igdb_import_state
            WHERE id = {IgdbImportState.SingletonId} AND claim_token = {lease.Token}
            FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (ownedState is null || ownedState.Version != lease.State.Version
            || ownedState.ClaimedUntil <= await GetDatabaseNowAsync(ct))
            throw new InvalidOperationException("IGDB import lease was lost before eligible games could load.");

        var gameMediaTypeId = await dbContext.Set<MediaType>()
            .Where(x => x.Name == "Video Game")
            .Select(x => x.Id)
            .SingleAsync(ct);

        var externalIds = eligible.Select(x => x.IgdbGameId.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var mediaByExternalId = await dbContext.Set<MediaEntity>()
            .Where(x => x.ExternalSource == MediaExternalSource.Igdb && x.ExternalId != null && externalIds.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId!, ct);
        var coverByLookupId = await dbContext.Set<MediaCover>()
            .Where(x => x.Provider == ArtworkProvider.Igdb && x.LookupKind == CoverLookupKind.IgdbGame && externalIds.Contains(x.LookupId))
            .ToDictionaryAsync(x => x.LookupId, ct);

        foreach (var staged in eligible)
        {
            var externalId = staged.IgdbGameId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!mediaByExternalId.TryGetValue(externalId, out var media))
            {
                media = new MediaEntity { ExternalId = externalId, ExternalSource = MediaExternalSource.Igdb, MediaTypeId = gameMediaTypeId };
                dbContext.Set<MediaEntity>().Add(media);
                mediaByExternalId.Add(externalId, media);
            }
            if (media.Id == 0 || media.UpdatedAt < staged.FetchedAt)
            {
                media.Title = staged.Name!;
                media.ReleaseDate = DateOnly.FromDateTime(staged.FirstReleaseDate!.Value.UtcDateTime);
            }

            if (!coverByLookupId.TryGetValue(externalId, out var cover))
            {
                cover = new MediaCover
                {
                    Provider = ArtworkProvider.Igdb,
                    LookupKind = CoverLookupKind.IgdbGame,
                    LookupId = externalId,
                    Version = 0
                };
                dbContext.Set<MediaCover>().Add(cover);
                coverByLookupId.Add(externalId, cover);
            }
            media.Cover = cover;

            // Do not overwrite a newer on-demand completion and do not extend a local staging reread.
            if (cover.CheckedAt is { } checkedAt && checkedAt >= staged.FetchedAt)
                continue;

            cover.ProviderItemId = staged.CoverImageId is null ? null : externalId;
            cover.ImagePath = staged.CoverImageId;
            cover.CheckedAt = staged.FetchedAt;
            cover.ExpiresAt = staged.FetchedAt.Add(staged.CoverImageId is null ? negativeCacheDuration : positiveCacheDuration);
            cover.Outcome = staged.CoverImageId is null ? CoverOutcome.Missing : CoverOutcome.Ready;
            cover.RequestedAt = null;
            cover.AttemptCount = 0;
            cover.FailureCode = null;
            cover.NextAttemptAt = cover.ExpiresAt;
            cover.ClaimToken = null;
            cover.ClaimedUntil = null;
            cover.Version++;
        }

        await dbContext.SaveChangesAsync(ct);
        var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE igdb_import_state
            SET claimed_until = clock_timestamp() + {lease.LeaseDuration},
                version = version + 1
            WHERE id = {IgdbImportState.SingletonId}
              AND claim_token = {lease.Token}
              AND claimed_until > clock_timestamp()
              AND version = {lease.State.Version}
            """, ct);
        if (updated != 1)
            throw new InvalidOperationException("IGDB import lease was lost before eligible games could load.");

        await transaction.CommitAsync(ct);
        dbContext.ChangeTracker.Clear();
        lease.State = await GetOwnedStateAsync(lease.Token, ct);
        return eligible.Count;
    }

    private async Task<IgdbImportState> GetOwnedStateAsync(Guid token, CancellationToken ct)
    {
        var databaseNow = await GetDatabaseNowAsync(ct);
        return await dbContext.Set<IgdbImportState>().AsNoTracking()
            .SingleAsync(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == token
                && x.ClaimedUntil > databaseNow, ct);
    }

    private IDisposable ApplyStatementTimeout()
    {
        var previousTimeout = dbContext.Database.GetCommandTimeout();
        var effectiveTimeout = previousTimeout is > 0
            ? Math.Min(previousTimeout.Value, maxStatementSeconds)
            : maxStatementSeconds;
        dbContext.Database.SetCommandTimeout(effectiveTimeout);
        return new CommandTimeoutRestore(dbContext, previousTimeout);
    }

    private sealed class CommandTimeoutRestore(PostgreSQLContext context, int? commandTimeout) : IDisposable
    {
        public void Dispose() => context.Database.SetCommandTimeout(commandTimeout);
    }
}
