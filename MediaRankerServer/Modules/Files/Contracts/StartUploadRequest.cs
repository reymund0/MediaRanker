namespace MediaRankerServer.Modules.Files.Contracts;

public class StartUploadRequest
{
    public string FileKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}
