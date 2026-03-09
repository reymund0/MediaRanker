using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaService(
    PostgreSQLContext dbContext,
    IValidator<MediaUpsertRequest> mediaUpsertRequestValidator
) : IMediaService
{
    public Task<List<MediaTypeDto>> GetMediaTypesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.MediaTypes
            .Select(mt => MediaTypeMapper.Map(mt))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MediaDto>> GetAllMediaAsync(CancellationToken cancellationToken = default)
    {
        var media = await dbContext.Media
            .AsNoTracking()
            .Include(m => m.MediaType)
            .ToListAsync(cancellationToken);

        return [.. media.Select(m => MediaMapper.Map(m))];
    }

    public async Task<MediaDto> CreateMediaAsync(MediaUpsertRequest request, CancellationToken cancellationToken = default)
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

        var media = new MediaEntity
        {
            Title = normalizedTitle,
            MediaTypeId = request.MediaTypeId,
            ReleaseDate = request.ReleaseDate
        };

        dbContext.Media.Add(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was created but could not be loaded.", "media_load_failed");
    }

    public async Task<MediaDto> UpdateMediaAsync(long mediaId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
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

        media.Title = normalizedTitle;
        media.MediaTypeId = request.MediaTypeId;
        media.ReleaseDate = request.ReleaseDate;
        media.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was updated but could not be loaded.", "media_load_failed");
    }

    public async Task DeleteMediaAsync(long mediaId, CancellationToken cancellationToken = default)
    {
        var media = await dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new DomainException("Media not found.", "media_not_found");

        dbContext.Media.Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MediaTypeDto?> GetMediaTypeByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var mediaType = await dbContext.MediaTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(mt => mt.Id == id, cancellationToken);

        return mediaType == null ? null : MediaTypeMapper.Map(mediaType);
    }

    private void ValidateMediaRequestOrThrow(MediaUpsertRequest request)
    {
        var validationResult = mediaUpsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "media_validation_error");
        }
    }

    private async Task<MediaDto?> GetMediaByIdAsync(long mediaId, CancellationToken cancellationToken)
    {
        var media = await dbContext.Media
            .AsNoTracking()
            .Include(m => m.MediaType)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        return media is null ? null : MediaMapper.Map(media);
    }
}
