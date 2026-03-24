namespace MediaRankerServer.Modules.Files.Contracts;

public class StartUploadResponse
{
    public long UploadId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
}
