using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public class UnreviewedMediaDto
{
  public long Id {get; set;}
  public string Title {get; set;} = null!;
  public DateOnly? ReleaseDate {get; set;}
  public string? CoverImageUrl {get; set;}
  public string CoverStatus { get; set; } = "unsupported";
}

public static class UnreviewedMediaDtoMapper
{
  public static UnreviewedMediaDto Map(MediaEntity media, CoverPresentation? cover = null)
  {
    cover ??= CoverPresentation.Unsupported;

    return new UnreviewedMediaDto
    {
      Id = media.Id,
      Title = media.Title,
      ReleaseDate = media.ReleaseDate,
      CoverImageUrl = cover.Url,
      CoverStatus = cover.Status
    };
  }
}
