using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public MediaTypeDto MediaType { get; set; } = new();
    public string? CoverImageUrl { get; set; }
}

public static class MediaDtoMapper
{
    public static async Task<MediaDto> MapAsync(MediaEntity media, IMediaCoverService coverService, CancellationToken cancellationToken = default)
    {
        string? mediaCoverUrl = null;
        if (media.CoverFileKey != null)
        {
            mediaCoverUrl = await coverService.GetCoverUrlAsync(media.CoverFileKey, cancellationToken);
        }
        return new MediaDto
        {
            Id = media.Id,
            Title = media.Title,
            MediaType = MediaTypeDtoMapper.Map(media.MediaType),
            ReleaseDate = media.ReleaseDate,
            CreatedAt = media.CreatedAt,
            UpdatedAt = media.UpdatedAt,
            CoverImageUrl = mediaCoverUrl
        };
    }
}
