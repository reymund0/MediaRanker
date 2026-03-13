namespace MediaRankerServer.Modules.Rankings.Contracts;

public class RankedMediaScoreUpsertRequest
{
  public long RankedMediaId { get; set; }
  public long TemplateFieldId { get; set; }
  public short Value { get; set; }
}