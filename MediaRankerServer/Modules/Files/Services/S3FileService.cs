using Amazon.S3;
using Amazon.S3.Model;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Data;
using FluentValidation;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Files.Services;

public class S3FileService(
    IAmazonS3 s3Client, 
    IValidator<StartUploadRequest> startUploadValidator, 
    IValidator<FinishUploadRequest> finishUploadValidator, 
    PostgreSQLContext dbContext, 
    IConfiguration configuration) : IFileService, IFileCleanupService
{
  private readonly S3DataProvider s3Provider = new(s3Client);

  public async Task<StartUploadResponse> StartUploadAsync(StartUploadRequest request, CancellationToken cancellationToken = default)
  {
    // Validate request
    var validationResult = startUploadValidator.Validate(request);
    if (!validationResult.IsValid)
    {
      throw new DomainException(validationResult.Errors[0].ErrorMessage, "file_validation_error");
    }

    // Generate file key
    var fileKey = Guid.NewGuid().ToString();

    // Insert upload record.
    var upload = new FileUpload
    {
      FileKey = fileKey,
      UserId = request.UserId,
      EntityType = Enum.Parse<FileEntityType>(request.EntityType),
      EntityId = request.EntityId,
      FileName = request.FileName,
      ExpectedContentType = request.ContentType,
      ExpectedFileSizeBytes = request.FileSizeBytes,
      CreatedAt = DateTime.UtcNow,
      State = FileUploadState.Uploading
    };
    dbContext.FileUploads.Add(upload);
    await dbContext.SaveChangesAsync(cancellationToken);

    // Generate upload URL
    var uploadUrl = s3Provider.CreateUploadUrl(fileKey, EntityTypeToBucket(upload.EntityType), upload.ExpectedContentType);

    return new StartUploadResponse
    {
      UploadId = upload.Id,
      UploadUrl = uploadUrl
    };
  }

  public async Task<FileDto> FinishUploadAsync(FinishUploadRequest request, CancellationToken cancellationToken = default)
  {
    // Validate request
    var validationResult = finishUploadValidator.Validate(request);
    if (!validationResult.IsValid)
    {
      throw new DomainException(validationResult.Errors[0].ErrorMessage, "file_validation_error");
    }

    // Validate upload exists.
    var upload = await dbContext.FileUploads.FindAsync([request.UploadId], cancellationToken);
    if (upload == null)
    {
      throw new DomainException("Upload not found", "upload_not_found");
    }
    // Validate upload belongs to user.
    else if (upload.UserId != request.UserId)
    {
      throw new DomainException("Upload does not belong to user", "upload_not_owned");
    }
    // Validate upload is in the correct state.
    else if (upload.State != FileUploadState.Uploading)
    {
      throw new DomainException("Upload is not in the correct state", "upload_invalid_state");
    }
    
    // Grab the object's metadata from S3, also doubles as validating the object exists.
    GetObjectMetadataResponse metadata;
    try {
        metadata = await s3Provider.GetObjectMetadataAsync(upload.FileKey, EntityTypeToBucket(upload.EntityType), cancellationToken);
    }
    catch
    {
        throw new DomainException("Failed to get object metadata", "s3_metadata_error");
    }
    

    // Update upload record with official metadata and transition state to uploaded.
    upload.ActualContentType = metadata.ContentType;
    upload.ActualFileSizeBytes = metadata.ContentLength;
    upload.State = FileUploadState.Uploaded;
    await dbContext.SaveChangesAsync(cancellationToken);
    
    return FileDtoMapper.Map(upload);
  }

  public async Task<FileDto> MarkUploadCopiedAsync(long uploadId, string userId, CancellationToken cancellationToken = default)
  {
    // Validate upload exists
    var upload = await dbContext.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId && u.UserId == userId, cancellationToken);
    if (upload == null)
    {
      throw new DomainException("Upload not found", "upload_not_found");
    }
    // Validate upload belongs to user.
    else if (upload.UserId != userId)
    {
      throw new DomainException("Upload does not belong to user", "upload_not_owned");
    }
    // Validate upload is in the correct state.
    else if (upload.State != FileUploadState.Uploaded)
    {
      throw new DomainException("Upload is not in the correct state", "upload_invalid_state");
    }
    
    // Transition state to Copied.
    upload.State = FileUploadState.Copied;
    await dbContext.SaveChangesAsync(cancellationToken);
    
    return FileDtoMapper.Map(upload);
  }

  public string GetFileUrl(string fileKey, FileEntityType entityType)
  {
    // For performance reasons we do not check if the file exists or is in a valid state.
    // The calling app is responsible for ensuring a valid fileKey is supplied.
    return s3Provider.CreatePreviewUrl(fileKey, EntityTypeToBucket(entityType), 3600);
  }

  public async Task DeleteFileAsync(string fileKey, FileEntityType entityType, CancellationToken cancellationToken = default)
  {
    // Find the File Upload record based on fileKey and entityType to confirm it's uploaded already.
    var file = await dbContext.FileUploads.FirstOrDefaultAsync(u => u.FileKey == fileKey && u.EntityType == entityType, cancellationToken);
    if (file == null)
    {
      throw new DomainException("File not found", "file_not_found");
    } 
    else if (file.State != FileUploadState.Copied && file.State != FileUploadState.Uploaded)
    {
      throw new DomainException("File is not in a deletable state", "file_invalid_state");
    }
    
    await s3Provider.DeleteObjectAsync(fileKey, EntityTypeToBucket(entityType), cancellationToken);
    
    // Transition state to Deleted.
    file.State = FileUploadState.Deleted;
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private string EntityTypeToBucket(FileEntityType entityType)
  {
    var key = entityType.ToString();
    return configuration.GetValue<string>($"AWS:S3:Buckets:{key}")
        ?? throw new InvalidOperationException($"Bucket not configured for entity type: {entityType}");
  }
}
