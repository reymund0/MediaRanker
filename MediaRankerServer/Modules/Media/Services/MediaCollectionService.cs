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
    IMediaCoverService coverService,
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
            .ToListAsync(cancellationToken);

        return [.. collections.Select(mc => MediaCollectionDtoMapper.Map(mc, fileService))];
    }

    public async Task<MediaCollectionDto?> GetCollectionByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var collection = await dbContext.MediaCollections
            .AsNoTracking()
            .Include(mc => mc.MediaType)
            .Include(mc => mc.ParentMediaCollection)
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken);

        return collection is null ? null : MediaCollectionDtoMapper.Map(collection, fileService);
    }

    public async Task<MediaCollectionDto> CreateCollectionAsync(string userId, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrThrowAsync(request, cancellationToken);

        var normalizedTitle = request.Title.Trim();

        FileDto? coverFile = null;
        if (request.CoverUploadId.HasValue)
        {
            coverFile = await coverService.CopyCoverFileAsync(userId, request.CoverUploadId.Value, cancellationToken);
        }

        var collection = new MediaCollection
        {
            Title = normalizedTitle,
            CollectionType = request.CollectionType,
            MediaTypeId = request.MediaTypeId,
            ParentMediaCollectionId = request.ParentMediaCollectionId,
            ReleaseDate = request.ReleaseDate,
            CoverFileUploadId = coverFile?.UploadId,
            CoverFileKey = coverFile?.FileKey,
            CoverFileName = coverFile?.FileName,
            CoverFileContentType = coverFile?.ContentType,
            CoverFileSizeBytes = coverFile?.FileSizeBytes,
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
            .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken)
            ?? throw new DomainException("Collection not found.", "collection_not_found");

        var normalizedTitle = request.Title.Trim();

        

        FileDto? coverFile = null;
        if (request.CoverUploadId.HasValue && collection.CoverFileUploadId != request.CoverUploadId.Value)
        {
            // Delete the old cover file if it exists.
            if (!string.IsNullOrEmpty(collection.CoverFileKey))
            {
                await coverService.DeleteCoverFileAsync(collection.CoverFileKey, cancellationToken);
            }

            coverFile = await coverService.CopyCoverFileAsync(userId, request.CoverUploadId.Value, cancellationToken);
        }

        collection.Title = normalizedTitle;
        collection.CollectionType = request.CollectionType;
        collection.MediaTypeId = request.MediaTypeId;
        collection.ParentMediaCollectionId = request.ParentMediaCollectionId;
        collection.ReleaseDate = request.ReleaseDate;

        if (coverFile != null)
        {
            collection.CoverFileUploadId = coverFile.UploadId;
            collection.CoverFileKey = coverFile.FileKey;
            collection.CoverFileName = coverFile.FileName;
            collection.CoverFileContentType = coverFile.ContentType;
            collection.CoverFileSizeBytes = coverFile.FileSizeBytes;
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
        
        if (!string.IsNullOrEmpty(collection.CoverFileKey))
        {
            await coverService.DeleteCoverFileAsync(collection.CoverFileKey, cancellationToken);
        }

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
        
        // Validate collection type specific rules.
        await ValidateCollectionTypeAsync(request, parent, cancellationToken);
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
