using FluentValidation;
using MediatR;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaService(
    PostgreSQLContext dbContext,
    IMediaCoverService coverService,
    IFileService fileService,
    IValidator<MediaUpsertRequest> mediaUpsertRequestValidator,
    IPublisher publisher
) : IMediaService
{
    public Task<List<MediaTypeDto>> GetMediaTypesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.MediaTypes
            .Select(mt => MediaTypeDtoMapper.Map(mt))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MediaDto>> GetAllMediaAsync(CancellationToken cancellationToken = default)
    {
        var media = await dbContext.Media
            .AsNoTracking()
            .Include(m => m.MediaType)
            .ToListAsync(cancellationToken);

        return [.. media.Select(m => MediaDtoMapper.Map(m, fileService))];
    }

    public async Task<MediaDto?> GetMediaByIdAsync(long mediaId, CancellationToken cancellationToken)
    {
        var media = await dbContext.Media
            .AsNoTracking()
            .Include(m => m.MediaType)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        return media is null ? null : MediaDtoMapper.Map(media, fileService);
    }

    public async Task<MediaDto> CreateMediaAsync(string userId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMediaRequestOrThrow(request);

        var normalizedTitle = request.Title.Trim();
        var duplicateExists = await dbContext.Media.AnyAsync(
            m => m.Title == normalizedTitle
                && m.MediaTypeId == request.MediaTypeId
                && m.ReleaseDate == request.ReleaseDate,
            cancellationToken
        );

        if (duplicateExists)
        {
            throw new DomainException("Media already exists for the selected title, media type, and release date.", "media_conflict");
        }

        // Get uploaded cover file info if provided.
        FileDto? coverFile = null;
        if (request.CoverUploadId.HasValue)
        {
            coverFile = await coverService.CopyCoverFileAsync(userId, request.CoverUploadId.Value, cancellationToken);
        }

        var media = new MediaEntity
        {
            Title = normalizedTitle,
            MediaTypeId = request.MediaTypeId,
            ReleaseDate = request.ReleaseDate,
            CoverFileUploadId = coverFile?.UploadId,
            CoverFileKey = coverFile?.FileKey,
            CoverFileName = coverFile?.FileName,
            CoverFileContentType = coverFile?.ContentType,
            CoverFileSizeBytes = coverFile?.FileSizeBytes,
        };

        dbContext.Media.Add(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was created but could not be loaded.", "media_load_failed");
    }

    public async Task<MediaDto> UpdateMediaAsync(string userId, long mediaId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMediaRequestOrThrow(request);

        var media = await dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new DomainException("Media not found.", "media_not_found");

        var normalizedTitle = request.Title.Trim();
        var duplicateExists = await dbContext.Media.AnyAsync(
            m => m.Id != mediaId
                && m.Title == normalizedTitle
                && m.MediaTypeId == request.MediaTypeId
                && m.ReleaseDate == request.ReleaseDate,
            cancellationToken
        );

        if (duplicateExists)
        {
            throw new DomainException("Media already exists for the selected title, media type, and release date.", "media_conflict");
        }

        // Get updated cover file info if provided.
        FileDto? coverFile = null;
        if (request.CoverUploadId.HasValue && media.CoverFileUploadId != request.CoverUploadId.Value)
        {
            // Delete the old cover file if it exists.
            if (!string.IsNullOrEmpty(media.CoverFileKey))
            {
                await coverService.DeleteCoverFileAsync(media.CoverFileKey, cancellationToken);
            }
            
            coverFile = await coverService.CopyCoverFileAsync(userId, request.CoverUploadId.Value, cancellationToken);
        }

        media.Title = normalizedTitle;
        media.MediaTypeId = request.MediaTypeId;
        media.ReleaseDate = request.ReleaseDate;
        media.CoverFileUploadId = coverFile?.UploadId;
        media.CoverFileKey = coverFile?.FileKey;
        media.CoverFileName = coverFile?.FileName;
        media.CoverFileContentType = coverFile?.ContentType;
        media.CoverFileSizeBytes = coverFile?.FileSizeBytes;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was updated but could not be loaded.", "media_load_failed");
    }

    public async Task DeleteMediaAsync(long mediaId, CancellationToken cancellationToken = default)
    {
        var media = await dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new DomainException("Media not found.", "media_not_found");

        // Delete the cover file if it exists
        if (!string.IsNullOrEmpty(media.CoverFileKey))
        {
            await coverService.DeleteCoverFileAsync(media.CoverFileKey, cancellationToken);
        }

        dbContext.Media.Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new MediaDeletedEvent(mediaId), cancellationToken);
    }

    public async Task<MediaTypeDto?> GetMediaTypeByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var mediaType = await dbContext.MediaTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(mt => mt.Id == id, cancellationToken);

        return mediaType == null ? null : MediaTypeDtoMapper.Map(mediaType);
    }

    private void ValidateMediaRequestOrThrow(MediaUpsertRequest request)
    {
        var validationResult = mediaUpsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "media_validation_error");
        }
    }
}
