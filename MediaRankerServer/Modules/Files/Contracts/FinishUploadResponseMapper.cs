using MediaRankerServer.Modules.Files.Entities;

namespace MediaRankerServer.Modules.Files.Contracts;

public static class FinishUploadResponseMapper
{
    public static FinishUploadResponse Map(InProgressUpload upload)
    {        
        return new FinishUploadResponse
        {
            UploadId = upload.Id,
            UserId = upload.UserId,
            EntityType = upload.EntityType,
            FileKey = upload.FileKey,
            FileName = upload.FileName,
            ContentType = upload.ActualContentType ?? throw new InvalidOperationException("Cannot finish upload without actual content type"),
            FileSize = upload.ActualFileSize ?? throw new InvalidOperationException("Cannot finish upload without actual file size"),
            UploadedAt = upload.UpdatedAt
        };
    }
}
