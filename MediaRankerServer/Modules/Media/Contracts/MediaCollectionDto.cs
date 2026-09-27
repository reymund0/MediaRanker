using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Data.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaCollectionDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CollectionType { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long? ParentMediaCollectionId { get; set; }
    public string? ParentMediaCollectionTitle { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CoverImageUrl { get; set; }
    public string CoverStatus { get; set; } = "unsupported";
}

public static class MediaCollectionDtoMapper
{
    public static MediaCollectionDto Map(MediaCollection collection, CoverPresentation? cover = null)
    {
        cover ??= CoverPresentation.Unsupported;

        return new MediaCollectionDto
        {
            Id = collection.Id,
            Title = collection.Title,
            CollectionType = collection.CollectionType.ToString(),
            MediaType = collection.MediaType,
            ParentMediaCollectionId = collection.ParentMediaCollectionId,
            ParentMediaCollectionTitle = collection.ParentMediaCollection?.Title,
            ReleaseDate = collection.ReleaseDate,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt,
            CoverImageUrl = cover.Url,
            CoverStatus = cover.Status
        };
    }
}
