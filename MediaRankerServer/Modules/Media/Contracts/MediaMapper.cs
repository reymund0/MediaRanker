using MediaRankerServer.Modules.Media.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public static class MediaMapper
{
    public static MediaDto Map(MediaEntity media)
    {
        return new MediaDto
        {
            Id = media.Id,
            Title = media.Title,
            MediaType = MediaTypeMapper.Map(media.MediaType),
            ReleaseDate = media.ReleaseDate,
            CreatedAt = media.CreatedAt,
            UpdatedAt = media.UpdatedAt
        };
    }
}
