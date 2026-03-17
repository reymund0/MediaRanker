namespace MediaRankerServer.Modules.Reviews.Contracts;

public class ReviewDto
{
  public long Id {get; set;}
  public string UserId { get; set; } = null!;
  public short OverallScore {get; set;}
  public string? ReviewTitle {get; set;}
  public string? Notes {get; set;}
  public DateTimeOffset? ConsumedAt { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }
  public List<ReviewFieldDto> Fields {get; set;} = [];
  // Template fields.
  public long TemplateId {get; set;}
  public string TemplateName {get; set;} = null!;
  // Media fields.
  public long MediaId {get; set;}
  public string MediaTitle {get; set;} = null!;
  public long MediaTypeId {get; set;}
  public string MediaTypeName {get; set;} = null!;
}
