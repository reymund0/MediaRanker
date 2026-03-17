namespace MediaRankerServer.Modules.Reviews.Contracts;

public class ReviewUpsertRequest
{
  public long? Id { get; set; }
  public string UserId { get; set; } = null!;
  public long MediaId { get; set; }
  public long TemplateId { get; set; }
  public string? ReviewTitle { get; set; }
  public string? Notes { get; set; }
  public DateTimeOffset? ConsumedAt { get; set; }
  public List<ReviewFieldUpsertRequest> Fields { get; set; } = [];    
}