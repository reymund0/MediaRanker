using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public sealed record IgdbImportRunResult(int PagesCommitted, int RowsCommitted, int EligibleGamesLoaded, bool RunCompleted, bool LeaseAcquired);

public sealed class IgdbImportService(
    IIgdbClient igdbClient,
    IIgdbImportProvider importProvider,
    IOptions<IgdbOptions> options,
    IOptions<ArtworkOptions> artworkOptions,
    ILogger<IgdbImportService> logger)
{
    private static readonly IReadOnlySet<string> SupportedGameTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Main game", "Remake", "Remaster"
    };

    private readonly IgdbOptions config = options.Value;
    private readonly ArtworkOptions artworkConfig = artworkOptions.Value;

    public async Task<IgdbImportRunResult> ImportAsync(CancellationToken ct = default)
    {
        if (!config.ImportEnabled)
        {
            logger.LogInformation("IGDB catalog import is disabled.");
            return new IgdbImportRunResult(0, 0, 0, false, false);
        }

        var now = DateTimeOffset.UtcNow;
        var lease = await importProvider.TryAcquireLeaseAsync(now, TimeSpan.FromSeconds(config.LeaseSeconds), ct);
        if (lease is null)
        {
            logger.LogInformation("Skipping IGDB import because another importer owns the durable lease.");
            return new IgdbImportRunResult(0, 0, 0, false, false);
        }

        try
        {
            var gameTypes = await igdbClient.GetGameTypesAsync(ct);
            var gameTypeNames = gameTypes.ToDictionary(x => x.Id, x => x.Name);
            var state = lease.State;
            if (!HasActiveRun(state))
            {
                if (!state.BootstrapCompleted)
                {
                    var maximumId = await igdbClient.GetMaximumGameIdAsync(ct);
                    state = await importProvider.StartRunAsync(lease, true, maximumId, null, null, now, ct);
                }
                else
                {
                    var completedWatermark = state.LastCompletedUpdatedAt ?? state.BootstrapStartedAt ?? now;
                    var lowerBound = completedWatermark.AddMinutes(-config.IncrementalOverlapMinutes);
                    state = await importProvider.StartRunAsync(lease, false, null, lowerBound, now, now, ct);
                }
            }

            var pagesCommitted = 0;
            var rowsCommitted = 0;
            var completed = false;
            while (pagesCommitted < config.PageBudget)
            {
                var page = await igdbClient.GetGamesAsync(new IgdbGameQuery(
                    state.LastCommittedId,
                    state.RunMaximumId,
                    state.RunUpdatedAfter,
                    state.RunUpdatedBefore,
                    config.PageSize), ct);

                if (page.Count == 0)
                {
                    await importProvider.CompleteRunAsync(lease, now, ct);
                    completed = true;
                    break;
                }

                var fetchedAt = DateTimeOffset.UtcNow;
                var staging = page.Select(game => new IgdbImport
                {
                    IgdbGameId = game.Id,
                    Name = game.Name?.Trim(),
                    FirstReleaseDate = game.FirstReleaseDate,
                    GameTypeId = game.GameTypeId,
                    GameTypeName = game.GameTypeId is { } typeId && gameTypeNames.TryGetValue(typeId, out var typeName) ? typeName : null,
                    VersionParentId = game.VersionParentId,
                    CoverImageId = IsValidCoverImageId(game.CoverImageId) ? game.CoverImageId : null,
                    ProviderUpdatedAt = game.UpdatedAt,
                    FetchedAt = fetchedAt,
                    CreatedAt = fetchedAt,
                    UpdatedAt = fetchedAt
                }).ToArray();

                state = await importProvider.CommitPageAsync(lease, staging, page[^1].Id, ct);
                pagesCommitted++;
                rowsCommitted += staging.Length;
                if (page.Count < config.PageSize)
                {
                    await importProvider.CompleteRunAsync(lease, now, ct);
                    completed = true;
                    break;
                }
            }

            // This is intentionally independent of upstream changes: future staged games become eligible on release day.
            var loaded = await importProvider.LoadEligibleGamesAsync(lease,
                SupportedGameTypeNames,
                DateTimeOffset.UtcNow,
                TimeSpan.FromDays(artworkConfig.PositiveCacheDays),
                TimeSpan.FromDays(artworkConfig.NegativeCacheDays),
                ct);
            return new IgdbImportRunResult(pagesCommitted, rowsCommitted, loaded, completed, true);
        }
        finally
        {
            await importProvider.ReleaseLeaseAsync(lease, ct);
        }
    }

    private static bool HasActiveRun(IgdbImportState state) => state.RunMaximumId is not null || state.RunUpdatedBefore is not null;

    private static bool IsValidCoverImageId(string? imageId) => imageId is { Length: > 0 and <= 255 }
        && imageId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
