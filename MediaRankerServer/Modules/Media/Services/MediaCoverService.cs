using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace MediaRankerServer.Modules.Media.Services;

public class MediaCoverService(
    IFileService fileService, 
    PostgreSQLContext dbContext, 
    IMediator mediator,
    IValidator<GenerateUploadCoverUrlRequest> validator,
    ILogger<MediaCoverService> logger) : IMediaCoverService, IMediaCoverCleanupService
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
    
    // Our cleanup is a 2-step process:
    // 1. Mark unreferenced covers for cleanup
    // 2. Delete covers that have been marked for cleanup
    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        // Find all media covers that are not referenced by any media or media collection.
        var unreferencedCovers = dbContext.MediaCovers
            .Where(mc => !dbContext.Media.Any(m => m.CoverId == mc.Id) && !dbContext.MediaCollections.Any(mc2 => mc2.CoverId == mc.Id));
        
        // For covers that are marked for deletion, we need to publish a FileDeleted event
        var coversToCleanup = await unreferencedCovers
            .Where(mc => mc.MarkedForCleanup)
            .ToListAsync(cancellationToken);
        
        await DeleteCoversAsync(coversToCleanup, cancellationToken);

        // Find covers to mark for cleanup (those that aren't already marked)
        await unreferencedCovers
            .Where(mc => !mc.MarkedForCleanup)
            .ExecuteUpdateAsync(setters => setters.SetProperty(mc => mc.MarkedForCleanup, true), cancellationToken);

        // Unmark covers that were previously marked for cleanup however are still referenced
        await dbContext.MediaCovers
            .Where(mc => mc.MarkedForCleanup)
            .Where(mc => dbContext.Media.Any(m => m.CoverId == mc.Id) || dbContext.MediaCollections.Any(mc2 => mc2.CoverId == mc.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(mc => mc.MarkedForCleanup, false), cancellationToken);
    }

    private async Task DeleteCoversAsync(List<MediaCover> covers, CancellationToken cancellationToken)
    {   
        List<MediaCover> coversToDelete = [];
        foreach (var cover in covers)
        {
            try{
                // Delete by publishing a FileDeletedEvent which Files module handles.
                await mediator.Publish(new FileDeletedEvent(cover.FileKey, FileEntityType.MediaCover.ToString()), cancellationToken);
                coversToDelete.Add(cover);
            }
            catch (Exception ex)
            {
                // Don't let 1 publish event stop us from continuing the cleanup process.
                logger.LogError(ex, "Error publishing FileDeleteEvent for cover {CoverId}", cover.Id);
            }
        }

        dbContext.MediaCovers.RemoveRange(coversToDelete);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
