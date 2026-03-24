using MediaRankerServer.Modules.Media.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public MediaTypeDto MediaType { get; set; } = new();
}

public static class MediaDtoMapper
{
    public static MediaDto Map(MediaEntity media)
    {
        return new MediaDto
        {
            Id = media.Id,
            Title = media.Title,
            MediaType = MediaTypeDtoMapper.Map(media.MediaType),
            ReleaseDate = media.ReleaseDate,
            CreatedAt = media.CreatedAt,
            UpdatedAt = media.UpdatedAt
        };
    }
}
