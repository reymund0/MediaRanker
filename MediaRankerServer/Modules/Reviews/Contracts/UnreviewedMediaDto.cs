using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;

namespace MediaRankerServer.Modules.Reviews.Contracts;

public class UnreviewedMediaDto
{
  public long Id {get; set;}
  public string Title {get; set;} = null!;
  public DateOnly ReleaseDate {get; set;}
  public string? CoverImageUrl {get; set;}
}

public static class UnreviewedMediaDtoMapper
{
  public static UnreviewedMediaDto Map(MediaEntity media, IFileService fileService)
  {
    string? coverImageUrl = null;
    if (media.CoverFileKey != null)
    {
      coverImageUrl = fileService.GetFileUrl(media.CoverFileKey, FileEntityType.MediaCover);
    }

    return new UnreviewedMediaDto
    {
      Id = media.Id,
      Title = media.Title,
      ReleaseDate = media.ReleaseDate,
      CoverImageUrl = coverImageUrl
    };
  }
}