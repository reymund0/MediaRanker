using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Files.Data.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long MediaTypeId { get; set; }
    public string MediaTypeName { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
}

public static class MediaDtoMapper
{
    public static MediaDto Map(MediaEntity media, IFileService fileService)
    {
        string? mediaCoverUrl = null;
        if (media.Cover != null)
        {
            mediaCoverUrl = fileService.GetFileUrl(media.Cover.FileKey, FileEntityType.MediaCover);
        }
        
        return new MediaDto
        {
            Id = media.Id,
            Title = media.Title,
            MediaTypeId = media.MediaTypeId,
            MediaTypeName = media.MediaType.Name,
            ReleaseDate = media.ReleaseDate,
            CreatedAt = media.CreatedAt,
            UpdatedAt = media.UpdatedAt,
            CoverImageUrl = mediaCoverUrl
        };
    }
}
