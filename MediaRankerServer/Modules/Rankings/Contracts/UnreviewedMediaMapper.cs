using MediaRankerServer.Modules.Media.Entities;

namespace MediaRankerServer.Modules.Rankings.Contracts;

public static class UnreviewedMediaMapper
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