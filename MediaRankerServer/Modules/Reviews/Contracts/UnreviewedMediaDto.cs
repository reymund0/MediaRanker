using MediaRankerServer.Modules.Media.Entities;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public class UnreviewedMediaDto
{
  public long Id {get; set;}
  public string Title {get; set;} = null!;
  public DateOnly ReleaseDate {get; set;}
}

public static class UnreviewedMediaDtoMapper
{
  public static UnreviewedMediaDto Map(MediaEntity media)
  {
    return new UnreviewedMediaDto
    {
      Id = media.Id,
      Title = media.Title,
      ReleaseDate = media.ReleaseDate
    };
  }
}