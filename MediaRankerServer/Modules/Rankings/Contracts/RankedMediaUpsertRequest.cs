namespace MediaRankerServer.Modules.Rankings.Contracts;

public class RankedMediaUpsertRequest
{
  public long? Id { get; set; }
  public string UserId { get; set; } = null!;
  public long MediaId { get; set; }
  public long TemplateId { get; set; }
  public string? ReviewTitle { get; set; }
  public string? Notes { get; set; }
  public DateTimeOffset? ConsumedAt { get; set; }
  public List<RankedMediaScoreUpsertRequest> Scores { get; set; } = [];    
}