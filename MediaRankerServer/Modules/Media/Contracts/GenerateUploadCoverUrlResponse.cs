namespace MediaRankerServer.Modules.Media.Contracts;

public class GenerateUploadCoverUrlResponse
{
    public string Url { get; set; } = string.Empty;
    public long UploadId { get; set; }
}
