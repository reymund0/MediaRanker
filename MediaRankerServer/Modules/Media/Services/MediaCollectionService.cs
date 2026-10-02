using FluentValidation;
using MediatR;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;
using SharedMediaType = MediaRankerServer.Shared.Data.MediaType;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaCollectionService(
    PostgreSQLContext dbContext,
    IArtworkService artworkService,
    IValidator<MediaCollectionUpsertRequest> validator,
    IPublisher publisher
) : IMediaCollectionService
{
    public async Task<PageResult<MediaCollectionDto>> GetAllCollectionsAsync(PageRequest request, CancellationToken cancellationToken = default, string? mediaType = null, string? collectionType = null, long? parentId = null)
    {
        if (mediaType is not null && !MediaTypes.IsValid(mediaType))
            throw new DomainException("Media type not found.", "media_type_not_found");
        MediaCollectionType? parsedCollectionType = null;
        if (collectionType is not null)
        {
            if (!Enum.TryParse<MediaCollectionType>(collectionType, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
                throw new DomainException("Collection type not found.", "collection_type_not_found");
            parsedCollectionType = parsed;
        }

        var v = PagingValidator.Validate(request, MediaCollectionQueryBuilder.SortFields, MediaCollectionQueryBuilder.SearchFields, "title");

        var query = MediaCollectionQueryBuilder.ApplyFilters(
            MediaCollectionQueryBuilder.BaseQuery(dbContext), mediaType, parsedCollectionType, parentId);
        var seasonCollection = parentId.HasValue && parsedCollectionType == MediaCollectionType.Season;
        if (seasonCollection)
            query = query.Where(mc => mc.MediaType == "TvShow" && mc.SeasonNumber.HasValue);
        query = MediaCollectionQueryBuilder.ApplySearch(query, v);
        int? totalCount = null;
        if (request.IncludeTotalCount == true)
            totalCount = await query.CountAsync(cancellationToken);
        var seriesSearch = parsedCollectionType == MediaCollectionType.Series && mediaType == "TvShow";
        query = seriesSearch
            ? MediaCollectionQueryBuilder.ApplySeriesRelevance(query, v, true)
            : MediaCollectionQueryBuilder.ApplySort(query, v);

        var page = seasonCollection
            ? await query.OrderBy(mc => mc.SeasonNumber).ThenBy(mc => mc.Id).ToListAsync(cancellationToken)
            : await query.Skip(v.Skip).Take(v.Take).ToListAsync(cancellationToken);
        var covers = await artworkService.GetCollectionArtworkAsync(page.Select(c => c.Id), cancellationToken);
        var items = page.Select(mc => MediaCollectionDtoMapper.Map(mc, covers?.GetValueOrDefault(mc.Id))).ToList();
        if (seriesSearch && page.Count > 0)
            await PopulateSeriesCountsAsync(page, items, cancellationToken);
        if (seasonCollection && page.Count > 0)
            await PopulateSeasonEpisodeCountsAsync(page, items, cancellationToken);

        return new PageResult<MediaCollectionDto>(
            items,
            seasonCollection ? page.Count : totalCount, v.Page, seasonCollection ? Math.Max(1, page.Count) : v.PageSize);
    }

    public async Task<MediaCollectionDto?> GetCollectionByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var collection = await MediaCollectionQueryBuilder.ApplyVisibility(MediaCollectionQueryBuilder.BaseQuery(dbContext))
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken);

        if (collection is null) return null;
        var covers = await artworkService.GetCollectionArtworkAsync([collection.Id], cancellationToken);
        var dto = MediaCollectionDtoMapper.Map(collection, covers?.GetValueOrDefault(collection.Id));
        if (collection.MediaType == "TvShow" && collection.CollectionType == MediaCollectionType.Series)
            await PopulateSeriesCountsAsync([collection], [dto], cancellationToken);
        if (collection.MediaType == "TvShow" && collection.CollectionType == MediaCollectionType.Season)
            await PopulateSeasonEpisodeCountsAsync([collection], [dto], cancellationToken);
        return dto;
    }

    public async Task<MediaCollectionDto> CreateCollectionAsync(string userId, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrThrowAsync(request, cancellationToken);

        var normalizedTitle = request.Title.Trim();
        var collection = new MediaCollection
        {
            Title = normalizedTitle,
            CollectionType = request.CollectionType,
            MediaType = request.MediaType,
            ParentMediaCollectionId = request.ParentMediaCollectionId,
            ReleaseDate = request.ReleaseDate
        };

        dbContext.MediaCollections.Add(collection);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetCollectionByIdAsync(collection.Id, cancellationToken)
            ?? throw new DomainException("Collection was created but could not be loaded.", "collection_load_failed");
    }

    public async Task<MediaCollectionDto> UpdateCollectionAsync(string userId, long id, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrThrowAsync(request, cancellationToken);

        var collection = await MediaCollectionQueryBuilder.ApplyVisibility(dbContext.MediaCollections
            .Include(mc => mc.ChildCollections)
            .Include(mc => mc.Cover))
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken)
            ?? throw new DomainException("Collection not found.", "collection_not_found");

        var normalizedTitle = request.Title.Trim();

        collection.Title = normalizedTitle;
        collection.CollectionType = request.CollectionType;
        collection.MediaType = request.MediaType;
        collection.ParentMediaCollectionId = request.ParentMediaCollectionId;
        collection.ReleaseDate = request.ReleaseDate;
        
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetCollectionByIdAsync(collection.Id, cancellationToken)
            ?? throw new DomainException("Collection was updated but could not be loaded.", "collection_load_failed");
    }

    public async Task DeleteCollectionAsync(long id, CancellationToken cancellationToken = default)
    {
        var collection = await dbContext.MediaCollections
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken)
            ?? throw new DomainException("Collection not found.", "collection_not_found");

        if (collection.MediaType == "TvShow" && collection.CollectionType == MediaCollectionType.Series)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var seasonIds = await dbContext.MediaCollections.AsNoTracking()
                .Where(mc => mc.ParentMediaCollectionId == id && mc.CollectionType == MediaCollectionType.Season)
                .Select(mc => mc.Id).ToListAsync(cancellationToken);
            var mediaToRemove = await GetSeriesRemovalMediaQuery(id).ToListAsync(cancellationToken);
            var eventMediaIds = mediaToRemove.Select(media => media.Id).Distinct().ToArray();
            var seasons = await dbContext.MediaCollections
                .Where(mc => seasonIds.Contains(mc.Id)).ToListAsync(cancellationToken);

            dbContext.Media.RemoveRange(mediaToRemove);
            dbContext.MediaCollections.RemoveRange(seasons);
            dbContext.MediaCollections.Remove(collection);
            await dbContext.SaveChangesAsync(cancellationToken);
            await publisher.Publish(new SeriesDeletedEvent(id, eventMediaIds), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        dbContext.MediaCollections.Remove(collection);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SeriesRemovalCountsDto> GetSeriesRemovalCountsAsync(long id, CancellationToken cancellationToken = default)
    {
        var collection = await dbContext.MediaCollections.AsNoTracking()
            .FirstOrDefaultAsync(mc => mc.Id == id && mc.MediaType == "TvShow" && mc.CollectionType == MediaCollectionType.Series, cancellationToken)
            ?? throw new DomainException("Collection not found.", "collection_not_found");
        var allMediaIds = GetSeriesRemovalMediaQuery(collection.Id).AsNoTracking().Select(media => media.Id);
        return new SeriesRemovalCountsDto
        {
            EpisodeCount = await allMediaIds.CountAsync(cancellationToken),
            ReviewCount = await dbContext.Reviews.AsNoTracking()
                .CountAsync(review => review.MediaCollectionId == collection.Id
                    || (review.MediaId.HasValue && allMediaIds.Contains(review.MediaId.Value)), cancellationToken)
        };
    }

    private async Task PopulateSeriesCountsAsync(
        IReadOnlyCollection<MediaCollection> series, IReadOnlyList<MediaCollectionDto> items, CancellationToken cancellationToken)
    {
        var ids = series.Select(item => item.Id).ToArray();
        var numberedSeasons = dbContext.MediaCollections.AsNoTracking()
            .Where(season => season.CollectionType == MediaCollectionType.Season
                && season.MediaType == "TvShow" && season.SeasonNumber.HasValue && season.ParentMediaCollectionId.HasValue
                && ids.Contains(season.ParentMediaCollectionId.Value));
        var seasonCounts = await numberedSeasons
            .GroupBy(season => season.ParentMediaCollectionId!.Value)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        var episodeCounts = await numberedSeasons
            .SelectMany(season => season.MediaItems.Where(media => media.MediaType == "TvShow"),
                (season, media) => new { SeriesId = season.ParentMediaCollectionId!.Value, media.Id })
            .GroupBy(row => row.SeriesId)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        var latestYears = await numberedSeasons
            .GroupBy(season => season.ParentMediaCollectionId!.Value)
            .Select(group => new
            {
                Id = group.Key,
                Year = group.OrderByDescending(season => season.SeasonNumber).ThenByDescending(season => season.Id)
                    .Select(season => season.ReleaseDate.HasValue ? (int?)season.ReleaseDate.Value.Year : null)
                    .FirstOrDefault()
            }).ToDictionaryAsync(row => row.Id, row => row.Year, cancellationToken);
        foreach (var dto in items)
        {
            dto.SeasonCount = seasonCounts.GetValueOrDefault(dto.Id, 0);
            dto.EpisodeCount = episodeCounts.GetValueOrDefault(dto.Id, 0);
            dto.EndYear = latestYears.GetValueOrDefault(dto.Id) ?? dto.StartYear;
        }
    }

    private IQueryable<MediaEntity> GetSeriesRemovalMediaQuery(long seriesId)
    {
        var seasonIds = dbContext.MediaCollections.AsNoTracking()
            .Where(mc => mc.ParentMediaCollectionId == seriesId && mc.CollectionType == MediaCollectionType.Season)
            .Select(mc => mc.Id);
        return dbContext.Media.Where(media => media.MediaCollectionId == seriesId
            || (media.MediaType == "TvShow" && media.MediaCollectionId.HasValue
                && seasonIds.Contains(media.MediaCollectionId.Value)));
    }

    private async Task PopulateSeasonEpisodeCountsAsync(
        IReadOnlyCollection<MediaCollection> seasons, IReadOnlyList<MediaCollectionDto> items, CancellationToken cancellationToken)
    {
        var ids = seasons.Select(season => season.Id).ToArray();
        var counts = await dbContext.Media.AsNoTracking()
            .Where(media => media.MediaType == "TvShow" && media.MediaCollectionId.HasValue
                && ids.Contains(media.MediaCollectionId.Value))
            .GroupBy(media => media.MediaCollectionId!.Value)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        foreach (var item in items) item.EpisodeCount = counts.GetValueOrDefault(item.Id, 0);
    }

    private async Task ValidateOrThrowAsync(MediaCollectionUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = validator.Validate(request);
        if (!result.IsValid)
        {
            throw new DomainException(result.Errors[0].ErrorMessage, "collection_validation_error");
        }

        // Validate parent exists if provided.
        var parent = await dbContext.MediaCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(mc => mc.Id == request.ParentMediaCollectionId, cancellationToken);
        if (parent == null && request.ParentMediaCollectionId.HasValue)
        {
            throw new DomainException("Parent collection not found.", "collection_parent_not_found");
        }
        
        ValidateCollectionParent(request, parent!);
        
        // Validate collection type specific rules.
        ValidateCollectionType(request, parent);

        if (request.MediaType == "TvShow" && request.CollectionType == MediaCollectionType.Season)
            throw new DomainException("TV seasons are managed by the IMDb import.", "collection_manual_season_unsupported");
    }

    private static void ValidateCollectionParent(MediaCollectionUpsertRequest request, MediaCollection parent)
    {
        // Validate parent is not the same as the collection being updated.
        if (parent != null && parent.Id == request.Id)
        {
            throw new DomainException("Parent collection cannot be the same as the collection being updated.", "collection_parent_self_reference");
        }

        // Validate parent media type matches child media type.
        if (parent != null && parent.MediaType != request.MediaType)
        {
            throw new DomainException("Parent collection media type does not match child media type.", "collection_parent_media_type_mismatch");
        }
    }

    private static void ValidateCollectionType(MediaCollectionUpsertRequest request, MediaCollection? parent)
    {
        var parsed = MediaTypes.Parse(request.MediaType);

        // Validate TV show collection type business rules.
        if (parsed == SharedMediaType.TvShow)
        {
            if (request.CollectionType == MediaCollectionType.Season)
            {
                // Validate that a Season cannot be created without being associated to a series.
                if (!request.ParentMediaCollectionId.HasValue)
                {
                    throw new DomainException(
                        "Season collections must be associated with a series collection.",
                        "collection_season_requires_series");
                }
                // Validate that the parent collection is a series.
                if (parent?.CollectionType != MediaCollectionType.Series)
                {
                    throw new DomainException(
                        "Season collections must be associated with a series collection.",
                        "collection_season_requires_series");
                }
            }
            // Validate that a Series does not have a parent collection.
            if (request.CollectionType == MediaCollectionType.Series && request.ParentMediaCollectionId.HasValue)
            {
                throw new DomainException(
                    "Series collections cannot have a parent collection.",
                    "collection_series_cannot_have_parent");
            }
        }
        else
        {
            // For other media types, we only are supporting series collections for now.
            if (request.CollectionType != MediaCollectionType.Series)
            {
                throw new DomainException(
                    "Unsupported collection type for this media type.",
                    "collection_type_unsupported");
            }
        }
    }
}
