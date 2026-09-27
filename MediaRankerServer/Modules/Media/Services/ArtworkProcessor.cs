using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public class ArtworkProcessor(PostgreSQLContext db, IIgdbClient igdbClient, ITmdbClient tmdbClient,
    IOptions<IgdbOptions> igdb, IOptions<TmdbOptions> tmdb, IOptions<ArtworkOptions> options,
    TimeProvider clock, ILogger<ArtworkProcessor> logger,
    IgdbRequestLimiter igdbLimiter, TmdbRequestCooldown tmdbCooldown)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        // Database-only expiry runs even when both upstream integrations are disabled.
        await db.MediaCovers.Where(x => x.Provider == ArtworkProvider.Tmdb && x.ExpiresAt <= now && (x.ImagePath != null || x.ProviderItemId != null))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ImagePath, (string?)null)
                .SetProperty(x => x.ProviderItemId, (string?)null).SetProperty(x => x.Outcome, CoverOutcome.Missing)
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        await db.MediaCovers.Where(x => x.AttemptCount >= settings.MaxAttempts && x.ClaimedUntil <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Outcome, CoverOutcome.Failed)
                .SetProperty(x => x.ClaimToken, (Guid?)null).SetProperty(x => x.ClaimedUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.RequestedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.NextAttemptAt, now.AddSeconds(settings.MaxRetrySeconds))
                .SetProperty(x => x.Version, x => x.Version + 1), ct);

        var igdbEnabled = igdb.Value.ArtworkEnabled && !string.IsNullOrWhiteSpace(igdb.Value.ClientId) && !string.IsNullOrWhiteSpace(igdb.Value.ClientSecret);
        var tmdbEnabled = tmdb.Value.Enabled && !string.IsNullOrWhiteSpace(tmdb.Value.ReadAccessToken);
        if (!igdbEnabled && !tmdbEnabled) return;
        for (var i = 0; i < settings.BatchSize; i++)
        {
            ct.ThrowIfCancellationRequested();
            var cover = await ClaimAsync(igdbEnabled && !igdbLimiter.IsBlocked,
                tmdbEnabled && !tmdbCooldown.IsBlocked, ct);
            if (cover is null) break;
            try
            {
                using var lookupDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                lookupDeadline.CancelAfter(TimeSpan.FromSeconds(settings.LeaseSeconds - 5));
                var result = cover.Provider == ArtworkProvider.Igdb
                    ? await igdbClient.GetCoverAsync(cover.LookupId, lookupDeadline.Token)
                    : await tmdbClient.GetCoverAsync(cover.LookupId, cover.LookupKind == CoverLookupKind.SeriesImdb, lookupDeadline.Token);
                var checkedAt = clock.GetUtcNow();
                var path = ArtworkPresentation.BuildUrl(cover.Provider, result.ImagePath) is null ? null : result.ImagePath;
                var expires = checkedAt.AddDays(path is null ? settings.NegativeCacheDays : settings.PositiveCacheDays);
                await db.MediaCovers.Where(x => x.Id == cover.Id && x.ClaimToken == cover.ClaimToken && x.Version == cover.Version)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Outcome, path == null ? CoverOutcome.Missing : CoverOutcome.Ready)
                        .SetProperty(x => x.ProviderItemId, result.ProviderItemId).SetProperty(x => x.ImagePath, path)
                        .SetProperty(x => x.CheckedAt, checkedAt).SetProperty(x => x.ExpiresAt, expires)
                        .SetProperty(x => x.RequestedAt, (DateTimeOffset?)null).SetProperty(x => x.NextAttemptAt, expires)
                        .SetProperty(x => x.AttemptCount, 0).SetProperty(x => x.FailureCode, (string?)null)
                        .SetProperty(x => x.ClaimToken, (Guid?)null).SetProperty(x => x.ClaimedUntil, (DateTimeOffset?)null)
                        .SetProperty(x => x.UpdatedAt, checkedAt).SetProperty(x => x.Version, x => x.Version + 1), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                // Provider clients sanitize failures; do not log request/response or credential payloads.
                var failure = exception as ProviderRequestException;
                var delay = TimeSpan.FromSeconds(Math.Min(settings.MaxRetrySeconds,
                    settings.RetrySeconds * Math.Pow(2, cover.AttemptCount - 1)));
                if (failure?.RetryAfter > delay) delay = failure.RetryAfter.Value;
                var retryAt = clock.GetUtcNow().Add(delay);
                var code = failure?.Code ?? "provider_unavailable";
                // A cooldown can begin after the claim but before HTTP; that is not a lookup attempt.
                var attemptCount = cover.AttemptCount - (failure?.IsLocalCooldown == true ? 1 : 0);
                await db.MediaCovers.Where(x => x.Id == cover.Id && x.ClaimToken == cover.ClaimToken && x.Version == cover.Version)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Outcome, CoverOutcome.Failed)
                        .SetProperty(x => x.AttemptCount, attemptCount)
                        .SetProperty(x => x.NextAttemptAt, retryAt).SetProperty(x => x.FailureCode, code)
                        .SetProperty(x => x.ClaimToken, (Guid?)null).SetProperty(x => x.ClaimedUntil, (DateTimeOffset?)null)
                        .SetProperty(x => x.Version, x => x.Version + 1), ct);
                logger.LogWarning("Artwork lookup failed for provider {Provider}; attempt {Attempt}", cover.Provider, attemptCount);
                if (code is "authentication_failed" or "invalid_configuration" or "rate_limited")
                {
                    if (cover.Provider == ArtworkProvider.Igdb) igdbEnabled = false;
                    else tmdbEnabled = false;
                }
            }
        }
    }

    private async Task<MediaCover?> ClaimAsync(bool igdbEnabled, bool tmdbEnabled, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var candidates = await db.MediaCovers.FromSqlInterpolated($"""
            SELECT * FROM media_covers
            WHERE requested_at IS NOT NULL AND next_attempt_at <= {now}
              AND (claimed_until IS NULL OR claimed_until <= {now}) AND attempt_count < {options.Value.MaxAttempts}
              AND ((provider = 'Igdb' AND {igdbEnabled}) OR (provider = 'Tmdb' AND {tmdbEnabled}))
            ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var cover = candidates.SingleOrDefault();
        if (cover is not null)
        {
            cover.ClaimToken = Guid.NewGuid();
            cover.ClaimedUntil = now.AddSeconds(options.Value.LeaseSeconds);
            cover.Outcome = CoverOutcome.Pending;
            cover.AttemptCount++;
            cover.Version++;
            await db.SaveChangesAsync(ct);
            db.Entry(cover).State = EntityState.Detached;
        }
        await transaction.CommitAsync(ct);
        return cover;
    }
}
