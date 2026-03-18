namespace MediaRankerServer.Modules.Media.Contracts;

public class GenerateUploadCoverUrlRequest
{
    public long? MediaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}
