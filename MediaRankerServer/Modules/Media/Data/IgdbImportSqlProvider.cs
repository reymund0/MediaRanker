using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Data;

public sealed class IgdbImportSqlProvider(PostgreSQLContext dbContext, ILogger<IgdbImportSqlProvider> logger) : IIgdbImportProvider
{
    public async Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct)
    {
        await dbContext.Database.ExecuteSqlRawAsync("INSERT INTO igdb_import_state (id, bootstrap_completed, run_is_bootstrap, last_committed_id, version) VALUES (1, FALSE, FALSE, 0, 0) ON CONFLICT (id) DO NOTHING", ct);

        var token = Guid.NewGuid();
        var claimedUntil = now.Add(leaseDuration);
        var acquired = await dbContext.Set<IgdbImportState>()
            .Where(x => x.Id == IgdbImportState.SingletonId && (x.ClaimedUntil == null || x.ClaimedUntil < now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ClaimToken, token)
                .SetProperty(x => x.ClaimedUntil, claimedUntil)
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        if (acquired == 0)
            return null;

        var state = await dbContext.Set<IgdbImportState>().AsNoTracking()
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
        var updated = await dbContext.Set<IgdbImportState>()
            .Where(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == lease.Token && x.ClaimedUntil > now && x.Version == lease.State.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RunIsBootstrap, bootstrap)
                .SetProperty(x => x.RunMaximumId, maximumId)
                .SetProperty(x => x.LastCommittedId, 0L)
                .SetProperty(x => x.RunUpdatedAfter, updatedAfter)
                .SetProperty(x => x.RunUpdatedBefore, updatedBefore)
                .SetProperty(x => x.BootstrapStartedAt, x => bootstrap && x.BootstrapStartedAt == null ? now : x.BootstrapStartedAt)
                .SetProperty(x => x.ClaimedUntil, now.Add(lease.LeaseDuration))
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        if (updated != 1)
            throw new InvalidOperationException("IGDB import lease was lost before the run could start.");

        lease.State = await GetOwnedStateAsync(lease.Token, ct);
        return lease.State;
    }

    public async Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long committedId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
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
            var updated = await dbContext.Set<IgdbImportState>()
                .Where(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == lease.Token && x.ClaimedUntil > now && x.Version == lease.State.Version)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LastCommittedId, committedId)
                    .SetProperty(x => x.ClaimedUntil, now.Add(lease.LeaseDuration))
                    .SetProperty(x => x.Version, x => x.Version + 1), ct);
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
            logger.LogError(ex, "IGDB staging page transaction failed without advancing its cursor.");
            throw;
        }
    }

    public async Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct)
    {
        var state = lease.State;
        var completionNow = DateTimeOffset.UtcNow;
        var updated = await dbContext.Set<IgdbImportState>()
            .Where(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == lease.Token && x.ClaimedUntil > completionNow && x.Version == state.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.BootstrapCompleted, x => state.RunIsBootstrap || x.BootstrapCompleted)
                .SetProperty(x => x.LastCompletedUpdatedAt, state.RunIsBootstrap ? (state.BootstrapStartedAt ?? now) : state.RunUpdatedBefore)
                .SetProperty(x => x.RunIsBootstrap, false)
                .SetProperty(x => x.RunMaximumId, (long?)null)
                .SetProperty(x => x.LastCommittedId, 0L)
                .SetProperty(x => x.RunUpdatedAfter, (DateTimeOffset?)null)
                .SetProperty(x => x.RunUpdatedBefore, (DateTimeOffset?)null)
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        if (updated != 1)
            throw new InvalidOperationException("IGDB import lease was lost before the run could complete.");

        lease.State = await GetOwnedStateAsync(lease.Token, ct);
    }

    public async Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct)
    {
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

    private async Task<int> LoadEligibleBatchAsync(IgdbImportLease lease, List<IgdbImport> eligible,
        TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var ownedState = await dbContext.Set<IgdbImportState>().FromSqlInterpolated($"""
            SELECT * FROM igdb_import_state
            WHERE id = {IgdbImportState.SingletonId} AND claim_token = {lease.Token} AND claimed_until > {now}
            FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (ownedState is null || ownedState.Version != lease.State.Version)
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

        ownedState.ClaimedUntil = DateTimeOffset.UtcNow.Add(lease.LeaseDuration);
        ownedState.Version++;
        await dbContext.SaveChangesAsync(ct);
        lease.State = ownedState;
        await transaction.CommitAsync(ct);
        dbContext.ChangeTracker.Clear();
        return eligible.Count;
    }

    private Task<IgdbImportState> GetOwnedStateAsync(Guid token, CancellationToken ct) => dbContext.Set<IgdbImportState>().AsNoTracking()
        .SingleAsync(x => x.Id == IgdbImportState.SingletonId && x.ClaimToken == token, ct);
}
