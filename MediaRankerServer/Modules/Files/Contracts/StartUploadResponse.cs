namespace MediaRankerServer.Modules.Files.Contracts;

public class StartUploadResponse
{
    public string UploadUrl { get; set; } = string.Empty;
    public string FileKey { get; set; } = string.Empty;
}
