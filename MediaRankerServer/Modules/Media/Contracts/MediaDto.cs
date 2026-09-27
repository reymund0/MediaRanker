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
    public string MediaType { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string CoverStatus { get; set; } = "unsupported";
}

public static class MediaDtoMapper
{
    public static MediaDto Map(MediaEntity media, CoverPresentation? cover = null)
    {
        cover ??= CoverPresentation.Unsupported;
        
        return new MediaDto
        {
            Id = media.Id,
            Title = media.Title,
            MediaType = media.MediaType,
            ReleaseDate = media.ReleaseDate,
            CreatedAt = media.CreatedAt,
            UpdatedAt = media.UpdatedAt,
            CoverImageUrl = cover.Url,
            CoverStatus = cover.Status
        };
    }
}
