using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public class ArtworkService(
    PostgreSQLContext db, IOptions<IgdbOptions> igdb, IOptions<TmdbOptions> tmdb,
    IOptions<ArtworkOptions> options, TimeProvider clock, ILogger<ArtworkService> logger) : IArtworkService
{
    private record Target(ArtworkProvider Provider, CoverLookupKind Kind, string Id, long? MediaId, long? SeriesId);

    public async Task<IReadOnlyDictionary<long, CoverPresentation>> GetMediaArtworkAsync(IEnumerable<long> mediaIds, CancellationToken ct = default)
    {
        var ids = mediaIds.Distinct().ToArray();
        try
        {
        var records = await db.Media.AsNoTracking().Include(x => x.MediaType)
            .Include(x => x.MediaCollection)!.ThenInclude(x => x!.ParentMediaCollection)
            .Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var targets = records.ToDictionary(x => x.Id, x => GetTarget(x));
        return await ResolveAsync(targets, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Could not read optional media artwork state");
            return ids.ToDictionary(x => x, _ => new CoverPresentation(null, "failed"));
        }
    }

    public async Task<IReadOnlyDictionary<long, CoverPresentation>> GetCollectionArtworkAsync(IEnumerable<long> collectionIds, CancellationToken ct = default)
    {
        var ids = collectionIds.Distinct().ToArray();
        try
        {
        var records = await db.MediaCollections.AsNoTracking().Include(x => x.ParentMediaCollection)
            .Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return await ResolveAsync(records.ToDictionary(x => x.Id, SeriesTarget), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Could not read optional collection artwork state");
            return ids.ToDictionary(x => x, _ => new CoverPresentation(null, "failed"));
        }
    }

    private static Target? GetTarget(MediaEntity media)
    {
        if (string.IsNullOrWhiteSpace(media.ExternalId)) return null;
        if (media.ExternalSource == MediaExternalSource.Igdb && media.MediaType.Name == "Video Game")
            return new(ArtworkProvider.Igdb, CoverLookupKind.IgdbGame, media.ExternalId, media.Id, null);
        if (media.ExternalSource != MediaExternalSource.Imdb) return null;
        return media.MediaType.Name switch
        {
            "Movie" => new(ArtworkProvider.Tmdb, CoverLookupKind.MovieImdb, media.ExternalId, media.Id, null),
            "TV Show" => SeriesTarget(media.MediaCollection),
            _ => null
        };
    }

    private static Target? SeriesTarget(MediaCollection? collection)
    {
        var series = collection?.CollectionType == MediaCollectionType.Series ? collection : collection?.ParentMediaCollection;
        return series is { CollectionType: MediaCollectionType.Series, ExternalSource: MediaExternalSource.Imdb }
            && !string.IsNullOrWhiteSpace(series.ExternalId)
            ? new(ArtworkProvider.Tmdb, CoverLookupKind.SeriesImdb, series.ExternalId, null, series.Id) : null;
    }

    private async Task<IReadOnlyDictionary<long, CoverPresentation>> ResolveAsync(Dictionary<long, Target?> targets, CancellationToken ct)
    {
        var result = new Dictionary<long, CoverPresentation>();
        var resolved = new Dictionary<Target, CoverPresentation>();
        foreach (var (id, target) in targets)
        {
            if (target is null) { result[id] = CoverPresentation.Unsupported; continue; }
            if (!resolved.TryGetValue(target, out var presentation))
            {
                try { presentation = await ResolveTargetAsync(target, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // Artwork is optional; never expose credentials or roll back a saved review.
                    logger.LogWarning("Could not register artwork demand for {Provider}/{Kind}", target.Provider, target.Kind);
                    presentation = new(null, "failed");
                }
                resolved[target] = presentation;
            }
            result[id] = presentation;
        }
        return result;
    }

    private bool Enabled(ArtworkProvider provider) => provider == ArtworkProvider.Igdb
        ? igdb.Value.ArtworkEnabled && !string.IsNullOrWhiteSpace(igdb.Value.ClientId) && !string.IsNullOrWhiteSpace(igdb.Value.ClientSecret)
        : tmdb.Value.Enabled && !string.IsNullOrWhiteSpace(tmdb.Value.ReadAccessToken);

    private async Task<CoverPresentation> ResolveTargetAsync(Target target, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cover = await FindAsync(target, ct);
        if (cover?.ExpiresAt > now && cover.Outcome is CoverOutcome.Ready or CoverOutcome.Missing)
        {
            await AttachOwnerAsync(target, cover, ct);
            return ArtworkPresentation.Map(cover, now);
        }
        if (!Enabled(target.Provider)) return new(null, "disabled");

        if (cover is null)
        {
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO media_covers (provider, lookup_kind, lookup_id, outcome, requested_at, next_attempt_at, attempt_count, version, created_at, updated_at)
                    VALUES ({target.Provider.ToString()}, {target.Kind.ToString()}, {target.Id}, 'Pending', {now}, {now}, 0, 0, {now}, {now})
                    ON CONFLICT (provider, lookup_kind, lookup_id) DO NOTHING
                    """, ct);
            }
            else
            {
                db.MediaCovers.Add(new MediaCover { Provider = target.Provider, LookupKind = target.Kind, LookupId = target.Id,
                    Outcome = CoverOutcome.Pending, RequestedAt = now, NextAttemptAt = now, CreatedAt = now, UpdatedAt = now });
                await db.SaveChangesAsync(ct);
            }
            cover = (await FindAsync(target, ct))!;
        }
        else if (cover.Outcome != CoverOutcome.Pending && (cover.NextAttemptAt is null || cover.NextAttemptAt <= now)
            && (cover.ClaimedUntil is null || cover.ClaimedUntil <= now))
        {
            if (db.Database.IsRelational())
            {
                var attempts = cover.AttemptCount >= options.Value.MaxAttempts ? 0 : cover.AttemptCount;
                await db.MediaCovers.Where(x => x.Id == cover.Id && x.Version == cover.Version)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Outcome, CoverOutcome.Pending)
                        .SetProperty(x => x.RequestedAt, now).SetProperty(x => x.NextAttemptAt, now)
                        .SetProperty(x => x.AttemptCount, attempts).SetProperty(x => x.ClaimToken, (Guid?)null)
                        .SetProperty(x => x.ClaimedUntil, (DateTimeOffset?)null).SetProperty(x => x.Version, x => x.Version + 1), ct);
            }
            else
            {
                var tracked = await db.MediaCovers.FindAsync([cover.Id], ct);
                tracked!.Outcome = CoverOutcome.Pending; tracked.RequestedAt = now; tracked.NextAttemptAt = now;
                if (tracked.AttemptCount >= options.Value.MaxAttempts) tracked.AttemptCount = 0;
                tracked.ClaimToken = null; tracked.ClaimedUntil = null; tracked.Version++;
                await db.SaveChangesAsync(ct);
            }
            cover = (await FindAsync(target, ct))!;
        }

        await AttachOwnerAsync(target, cover, ct);
        return ArtworkPresentation.Map(cover, now);
    }

    private async Task AttachOwnerAsync(Target target, MediaCover cover, CancellationToken ct)
    {
        // Only canonical owners store the association; TV descendants resolve through their series.
        if (db.Database.IsRelational())
        {
            if (target.MediaId is long mediaId)
                await db.Media.Where(x => x.Id == mediaId && x.ExternalId == target.Id && x.CoverId != cover.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CoverId, cover.Id), ct);
            if (target.SeriesId is long seriesId)
                await db.MediaCollections.Where(x => x.Id == seriesId && x.ExternalId == target.Id && x.CoverId != cover.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CoverId, cover.Id), ct);
        }
        else
        {
            if (target.MediaId is long mediaId)
            {
                var media = await db.Media.FindAsync([mediaId], ct);
                if (media?.ExternalId == target.Id) media.CoverId = cover.Id;
            }
            if (target.SeriesId is long seriesId)
            {
                var series = await db.MediaCollections.FindAsync([seriesId], ct);
                if (series?.ExternalId == target.Id) series.CoverId = cover.Id;
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private Task<MediaCover?> FindAsync(Target target, CancellationToken ct) => db.MediaCovers.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Provider == target.Provider && x.LookupKind == target.Kind && x.LookupId == target.Id, ct);
}
