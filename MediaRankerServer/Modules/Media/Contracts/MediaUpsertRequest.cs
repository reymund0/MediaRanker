namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaUpsertRequest
{
    public long? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public long MediaTypeId { get; set; }
    public DateOnly ReleaseDate { get; set; }
}
