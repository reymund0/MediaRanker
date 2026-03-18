namespace MediaRankerServer.Modules.Files.Contracts;

public class FinishUploadResponse
{
    public long UploadId { get; set; }
    public string UserId { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string FileKey { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
