using MediaRankerServer.Modules.Media.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public static class MediaTypeMapper
{
    public static MediaTypeDto Map(MediaType mediaType)
    {
        return new MediaTypeDto
        {
            Id = mediaType.Id,
            Name = mediaType.Name,
        };
    }
}
