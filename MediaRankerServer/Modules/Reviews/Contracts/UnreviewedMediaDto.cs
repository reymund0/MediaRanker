namespace MediaRankerServer.Modules.Reviews.Contracts;

public class UnreviewedMediaDto
{
  public long Id {get; set;}
  public string Title {get; set;} = null!;
  public DateOnly ReleaseDate {get; set;}
}
