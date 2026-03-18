namespace MediaRankerServer.Modules.Media.Contracts;

public class CompleteUploadCoverRequest
{
    public long? MediaId { get; set; }
    public string FileKey { get; set; } = string.Empty;
}
