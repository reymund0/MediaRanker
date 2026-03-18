namespace MediaRankerServer.Modules.Files.Contracts;

public class FinishUploadRequest
{
    public string FileKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
}
