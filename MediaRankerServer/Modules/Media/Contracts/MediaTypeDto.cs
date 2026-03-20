using MediaRankerServer.Modules.Media.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaTypeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}


public static class MediaTypeDtoMapper
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
