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
    public int? EpisodeNumber { get; set; }
    public int? SeasonNumber { get; set; }
    public long? SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
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
            EpisodeNumber = media.EpisodeNumber,
            SeasonNumber = media.MediaCollection?.CollectionType == MediaCollectionType.Season ? media.MediaCollection.SeasonNumber : null,
            SeriesId = media.MediaCollection?.CollectionType == MediaCollectionType.Season
                ? media.MediaCollection.ParentMediaCollectionId
                : media.MediaCollection?.CollectionType == MediaCollectionType.Series ? media.MediaCollection.Id : null,
            SeriesTitle = media.MediaCollection?.CollectionType == MediaCollectionType.Season
                ? media.MediaCollection.ParentMediaCollection?.Title
                : media.MediaCollection?.CollectionType == MediaCollectionType.Series ? media.MediaCollection.Title : null,
            ReleaseDate = media.ReleaseDate,
            CreatedAt = media.CreatedAt,
            UpdatedAt = media.UpdatedAt,
            CoverImageUrl = cover.Url,
            CoverStatus = cover.Status
        };
    }
}
