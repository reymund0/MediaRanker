using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediatR;

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
        
        return;
    }

    public async Task<FileDto> CopyCoverFileAsync(string userId, long uploadId, CancellationToken cancellationToken)
    {
        return await fileService.MarkUploadCopiedAsync(uploadId, userId, cancellationToken);
    }

    public async Task DeleteCoverFileAsync(string fileKey, CancellationToken cancellationToken)
    {
        // We delete by publishing a FileDeletedEvent which S3FileService (or cleanup) handles.
        await mediator.Publish(new FileDeletedEvent(fileKey, FileEntityType.MediaCover.ToString()), cancellationToken);
    }
}
