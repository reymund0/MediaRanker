using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediaRankerServer.Modules.Media.Data.Entities;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaCoverService(
    IFileService fileService, 
    PostgreSQLContext dbContext, 
    IMediator mediator,
    IValidator<GenerateUploadCoverUrlRequest> validator) : IMediaCoverService
{
    public async Task<GenerateUploadCoverUrlResponse> GenerateUploadCoverUrlAsync(string userId, GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "media_cover_validation_error");
        }

        // If mediaId was provided ensure it exists.
        if (request.MediaId.HasValue)
        {
            _ = dbContext.Media.FirstOrDefault(m => m.Id == request.MediaId.Value) ??
                throw new DomainException("Media not found.", "media_not_found");
        }

        // Generate a presigned URL for the file
        var result = await fileService.StartUploadAsync(new StartUploadRequest
        {
            UserId = userId,
            EntityId = request.MediaId,
            EntityType = FileEntityType.MediaCover.ToString(),
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            ContentType = request.ContentType
        }, cancellationToken);

        // Return the presigned URL and file key
        return new GenerateUploadCoverUrlResponse
        {
            Url = result.UploadUrl,
            UploadId = result.UploadId
        };
    }

    public async Task CompleteUploadCoverAsync(string userId, long uploadId, CancellationToken cancellationToken)
    {
        // Finish the upload.
        var uploadedFile = await fileService.FinishUploadAsync(new FinishUploadRequest
        {
            UploadId = uploadId,
            UserId = userId
        }, cancellationToken);

        // Validate user actually uploaded an image. If not, delete the file and throw an exception.
        if (!uploadedFile.ContentType.StartsWith("image/"))
        {
            await mediator.Publish(new FileDeletedEvent(uploadedFile.FileKey, FileEntityType.MediaCover.ToString()), cancellationToken);
            throw new DomainException("Only image files are allowed.", "invalid_file_type");
        }

        // Now copy the file data into Media Covers table.
        var cover = new MediaCover
        {
            FileUploadId = uploadId,
            FileKey = uploadedFile.FileKey,
            FileName = uploadedFile.FileName,
            FileSizeBytes = uploadedFile.FileSizeBytes,
            FileContentType = uploadedFile.ContentType
        };
        dbContext.MediaCovers.Add(cover);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Signal that the upload has been copied to the database.
        await fileService.MarkUploadCopiedAsync(uploadId, userId, cancellationToken);
    }

    private async Task DeleteCoverFileIfUnusedAsync(long coverId, CancellationToken cancellationToken)
    {
        // Check if any media or media collections still references this cover.
        var mediaCount = await dbContext.Media.CountAsync(m => m.CoverId == coverId, cancellationToken);
        var mediaCollectionCount = await dbContext.MediaCollections.CountAsync(mc => mc.CoverId == coverId, cancellationToken);

        if (mediaCount > 0 || mediaCollectionCount > 0)
        {
            return;
        }

        // Get the file key for the cover.
        var cover = await dbContext.MediaCovers.FirstOrDefaultAsync(mc => mc.Id == coverId, cancellationToken);
        if (cover == null)
        {
            return;
        }
        
        // Delete by publishing a FileDeletedEvent which Files module handles.
        await mediator.Publish(new FileDeletedEvent(cover.FileKey, FileEntityType.MediaCover.ToString()), cancellationToken);
    }
}
