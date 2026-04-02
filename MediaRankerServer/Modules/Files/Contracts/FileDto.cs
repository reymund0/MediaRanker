using MediaRankerServer.Modules.Files.Data.Entities;

namespace MediaRankerServer.Modules.Files.Contracts;

public class FileDto
{
    public long UploadId { get; set; }
    public string UserId { get; set; } = null!;
    public long? EntityId { get; set; }
    public string EntityType { get; set; } = null!;
    public string FileKey { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}


public static class FileDtoMapper
{
    public static FileDto Map(FileUpload upload)
    {        
        return new FileDto
        {
            UploadId = upload.Id,
            UserId = upload.UserId,
            EntityId = upload.EntityId,
            EntityType = upload.EntityType.ToString(),
            FileKey = upload.FileKey,
            FileName = upload.FileName,
            ContentType = upload.ActualContentType ?? throw new InvalidOperationException("Cannot finish upload without actual content type"),
            FileSizeBytes = upload.ActualFileSizeBytes ?? throw new InvalidOperationException("Cannot finish upload without actual file size"),
            UploadedAt = upload.UpdatedAt
        };
    }
}