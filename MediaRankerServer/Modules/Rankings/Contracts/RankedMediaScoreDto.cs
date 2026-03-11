namespace MediaRankerServer.Modules.Rankings.Contracts;

public class RankedMediaScoreDto {
  public long RankedMediaId { get; set; }
  public long TemplateFieldId { get; set; }
  public short Value { get; set; } 
}