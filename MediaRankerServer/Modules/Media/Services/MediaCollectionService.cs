using FluentValidation;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaCollectionService(
    PostgreSQLContext dbContext,
    IFileService fileService,
    IValidator<MediaCollectionUpsertRequest> validator
) : IMediaCollectionService
{
    public async Task<List<MediaCollectionDto>> GetAllCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await dbContext.MediaCollections
            .AsNoTracking()
            .Include(mc => mc.MediaType)
            .Include(mc => mc.ParentMediaCollection)
            .Include(mc => mc.Cover)
            .ToListAsync(cancellationToken);

        return [.. collections.Select(mc => MediaCollectionDtoMapper.Map(mc, fileService))];
    }

    public async Task<MediaCollectionDto?> GetCollectionByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var collection = await dbContext.MediaCollections
            .AsNoTracking()
            .Include(mc => mc.MediaType)
            .Include(mc => mc.ParentMediaCollection)
            .Include(mc => mc.Cover)
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken);

        return collection is null ? null : MediaCollectionDtoMapper.Map(collection, fileService);
    }

    public async Task<MediaCollectionDto> CreateCollectionAsync(string userId, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrThrowAsync(request, cancellationToken);

        var normalizedTitle = request.Title.Trim();
        var requestCoverId = dbContext.MediaCovers.FirstOrDefault(c => c.FileUploadId == request.CoverUploadId)?.Id;
        var collection = new MediaCollection
        {
            Title = normalizedTitle,
            CollectionType = request.CollectionType,
            MediaTypeId = request.MediaTypeId,
            ParentMediaCollectionId = request.ParentMediaCollectionId,
            ReleaseDate = request.ReleaseDate,
            CoverId = requestCoverId
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
        
        // If the cover ID was updated, then we need to update all referenced entities.
        if (collection.Cover?.FileUploadId != request.CoverUploadId)
        {
            // We know newMediaCover is not null at this point because we validated it in our upsert validator.
            var newMediaCoverId = (await dbContext.MediaCovers
                .FirstOrDefaultAsync(mc => mc.FileUploadId == request.CoverUploadId, cancellationToken))!.Id;
            await CascadeUpdateMediaCoverAsync(collection, newMediaCoverId, cancellationToken);

            // Update the coverID to the new value.
            collection.CoverId = newMediaCoverId;
        }

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

    private async Task CascadeUpdateMediaCoverAsync(MediaCollection collection, long newCoverId, CancellationToken cancellationToken)
    {
        // Find all the child collections and media that reference the old cover ID and update them to use the new cover ID.
        // The only exception is if the child entity has a unique coverId, then we won't change it because it has a unique cover.
        
        // Track which collections we've updated so we can update all Media at the same time.
        List<long> updatedCollectionIds = [collection.Id];
        var oldCoverId = collection.CoverId;

        // Update child collections that reference the old cover ID.
        if (collection.ChildCollections.Count > 0)
        {
            foreach (var childCollection in collection.ChildCollections)
            {
                if (childCollection.CoverId == oldCoverId)
                {
                    childCollection.CoverId = newCoverId;
                    updatedCollectionIds.Add(childCollection.Id);
                }
            }
        }
        // Update all media items referencing the old cover ID for the collections we've updated.
        await dbContext.Media
            .Where(m => m.MediaCollectionId != null)
            .Where(m => updatedCollectionIds.Contains(m.MediaCollectionId!.Value))
            .Where(m => m.CoverId == oldCoverId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CoverId, newCoverId), cancellationToken);
    }

    private async Task ValidateOrThrowAsync(MediaCollectionUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = validator.Validate(request);
        if (!result.IsValid)
        {
            throw new DomainException(result.Errors[0].ErrorMessage, "collection_validation_error");
        }

        // Validate cover exists if provided.
        if (request.CoverUploadId.HasValue)
        {
            var cover = await dbContext.MediaCovers.FirstOrDefaultAsync(mc => mc.FileUploadId == request.CoverUploadId.Value, cancellationToken)
                ?? throw new DomainException("Cover not found.", "cover_not_found");
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
            if (request.CollectionType == CollectionType.Season)
            {
                // Validate that a Season cannot be created without being associated to a series.
                if (!request.ParentMediaCollectionId.HasValue)
                {
                    throw new DomainException(
                        "Season collections must be associated with a series collection.",
                        "collection_season_requires_series");
                }
                // Validate that the parent collection is a series.
                if (parent?.CollectionType != CollectionType.Series)
                {
                    throw new DomainException(
                        "Season collections must be associated with a series collection.",
                        "collection_season_requires_series");
                }
            }
            // Validate that a Series does not have a parent collection.
            if (request.CollectionType == CollectionType.Series && request.ParentMediaCollectionId.HasValue)
            {
                throw new DomainException(
                    "Series collections cannot have a parent collection.",
                    "collection_series_cannot_have_parent");
            }
        }
        else {
            // For other media types, we only are supporting series collections for now.
            if (request.CollectionType != CollectionType.Series)
            {
                throw new DomainException(
                    "Unsupported collection type for this media type.",
                    "collection_type_unsupported");
            }
        }
    }
}
