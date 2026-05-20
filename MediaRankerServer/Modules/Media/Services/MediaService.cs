using FluentValidation;
using MediatR;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaService(
    PostgreSQLContext dbContext,
    IFileService fileService,
    IValidator<MediaUpsertRequest> mediaUpsertRequestValidator,
    IPublisher publisher
) : IMediaService
{

    public async Task<PageResult<MediaDto>> GetAllMediaAsync(string? mediaType, PageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(mediaType))
            throw new DomainException("Media type is required.", "media_validation_error");
        if (!MediaTypes.IsValid(mediaType))
            throw new DomainException("Media type not found.", "media_type_not_found");

        var v = PagingValidator.Validate(request, MediaQueryBuilder.SortFields, MediaQueryBuilder.SearchFields, "title");

        var query = MediaQueryBuilder.ApplySearch(
            MediaQueryBuilder.BaseQuery(dbContext).Where(m => m.MediaType == mediaType), v);
        int? totalCount = null;
        if (request.IncludeTotalCount == true) 
            totalCount = await query.CountAsync(cancellationToken);
            
        query = MediaQueryBuilder.ApplySort(query, v);

        var page = await query.Skip(v.Skip).Take(v.Take).ToListAsync(cancellationToken);

        return new PageResult<MediaDto>(
            [.. page.Select(m => MediaDtoMapper.Map(m, fileService))],
            totalCount, v.Page, v.PageSize);
    }

    public async Task<MediaDto?> GetMediaByIdAsync(long mediaId, CancellationToken cancellationToken)
    {
        var media = await MediaQueryBuilder.BaseQuery(dbContext)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        return media is null ? null : MediaDtoMapper.Map(media, fileService);
    }

    public async Task<MediaDto> CreateMediaAsync(string userId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateMediaRequestOrThrow(request, cancellationToken);

        var normalizedTitle = request.Title.Trim();
        var duplicateExists = await dbContext.Media.AnyAsync(
            m => m.Title == normalizedTitle
                && m.MediaType == request.MediaType
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
            MediaType = request.MediaType,
            ReleaseDate = request.ReleaseDate,
            CoverId = dbContext.MediaCovers.FirstOrDefault(c => c.FileUploadId == request.CoverUploadId)?.Id
        };

        dbContext.Media.Add(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was created but could not be loaded.", "media_load_failed");
    }

    public async Task<MediaDto> UpdateMediaAsync(string userId, long mediaId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateMediaRequestOrThrow(request, cancellationToken);

        var media = await dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new DomainException("Media not found.", "media_not_found");

        var normalizedTitle = request.Title.Trim();
        var duplicateExists = await dbContext.Media.AnyAsync(
            m => m.Id != mediaId
                && m.Title == normalizedTitle
                && m.MediaType == request.MediaType
                && m.ReleaseDate == request.ReleaseDate,
            cancellationToken
        );

        if (duplicateExists)
        {
            throw new DomainException("Media already exists for the selected title, media type, and release date.", "media_conflict");
        }

        media.Title = normalizedTitle;
        media.MediaType = request.MediaType;
        media.ReleaseDate = request.ReleaseDate;
        media.CoverId = dbContext.MediaCovers.FirstOrDefault(c => c.FileUploadId == request.CoverUploadId)?.Id;

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

        await publisher.Publish(new MediaDeletedEvent(mediaId), cancellationToken);
    }

    private async Task ValidateMediaRequestOrThrow(MediaUpsertRequest request, CancellationToken cancellationToken)
    {
        var validationResult = mediaUpsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "media_validation_error");
        }

        // Validate Cover exists if provided
        if (request.CoverUploadId.HasValue)
        {
            var coverExists = await dbContext.MediaCovers.AnyAsync(c => c.FileUploadId == request.CoverUploadId, cancellationToken);
            if (!coverExists)
            {
                throw new DomainException("Cover not found.", "cover_not_found");
            }
        }
    }
}
