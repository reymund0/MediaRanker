namespace MediaRankerServer.Modules.Reviews.Contracts;

public class ReviewFieldUpsertRequest
{
  public long ReviewId { get; set; }
  public long TemplateFieldId { get; set; }
  public short Value { get; set; }
}