using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaCollectionService(
    PostgreSQLContext dbContext,
    IArtworkService artworkService,
    IValidator<MediaCollectionUpsertRequest> validator
) : IMediaCollectionService
{
    public async Task<PageResult<MediaCollectionDto>> GetAllCollectionsAsync(PageRequest request, CancellationToken cancellationToken = default)
    {
        var v = PagingValidator.Validate(request, MediaCollectionQueryBuilder.SortFields, MediaCollectionQueryBuilder.SearchFields, "title");

        var query = MediaCollectionQueryBuilder.ApplySearch(MediaCollectionQueryBuilder.BaseQuery(dbContext), v);
        int? totalCount = null;
        if (request.IncludeTotalCount == true)
            totalCount = await query.CountAsync(cancellationToken);
        query = MediaCollectionQueryBuilder.ApplySort(query, v);

        var page = await query.Skip(v.Skip).Take(v.Take).ToListAsync(cancellationToken);
        var covers = await artworkService.GetCollectionArtworkAsync(page.Select(c => c.Id), cancellationToken);

        return new PageResult<MediaCollectionDto>(
            [.. page.Select(mc => MediaCollectionDtoMapper.Map(mc, covers?.GetValueOrDefault(mc.Id)))],
            totalCount, v.Page, v.PageSize);
    }

    public async Task<MediaCollectionDto?> GetCollectionByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var collection = await MediaCollectionQueryBuilder.BaseQuery(dbContext)
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken);

        if (collection is null) return null;
        var covers = await artworkService.GetCollectionArtworkAsync([collection.Id], cancellationToken);
        return MediaCollectionDtoMapper.Map(collection, covers?.GetValueOrDefault(collection.Id));
    }

    public async Task<MediaCollectionDto> CreateCollectionAsync(string userId, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrThrowAsync(request, cancellationToken);

        var normalizedTitle = request.Title.Trim();
        var collection = new MediaCollection
        {
            Title = normalizedTitle,
            CollectionType = request.CollectionType,
            MediaTypeId = request.MediaTypeId,
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

        var collection = await dbContext.MediaCollections
            .Include(mc => mc.ChildCollections)
            .Include(mc => mc.Cover)
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken)
            ?? throw new DomainException("Collection not found.", "collection_not_found");

        var normalizedTitle = request.Title.Trim();

        collection.Title = normalizedTitle;
        collection.CollectionType = request.CollectionType;
        collection.MediaTypeId = request.MediaTypeId;
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

        dbContext.MediaCollections.Remove(collection);
        await dbContext.SaveChangesAsync(cancellationToken);
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
        await ValidateCollectionTypeAsync(request, parent, cancellationToken);
    }

    private static void ValidateCollectionParent(MediaCollectionUpsertRequest request, MediaCollection parent)
    {
        // Validate parent is not the same as the collection being updated.
        if (parent != null && parent.Id == request.Id)
        {
            throw new DomainException("Parent collection cannot be the same as the collection being updated.", "collection_parent_self_reference");
        }

        // Validate parent media type matches child media type.
        if (parent != null && parent.MediaTypeId != request.MediaTypeId)
        {
            throw new DomainException("Parent collection media type does not match child media type.", "collection_parent_media_type_mismatch");
        }
    }

    private async Task ValidateCollectionTypeAsync(MediaCollectionUpsertRequest request, MediaCollection? parent, CancellationToken cancellationToken)
    {
        var mediaType = await dbContext.MediaTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(mt => mt.Id == request.MediaTypeId, cancellationToken)
            ?? throw new DomainException("Media type not found.", "media_type_not_found");

        // Validate TV show collection type business rules.
        if (mediaType.Name == "TV Show")
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
        else {
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
