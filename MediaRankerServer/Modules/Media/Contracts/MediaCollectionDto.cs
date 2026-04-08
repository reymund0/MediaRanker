using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Media.Data.Entities;

namespace MediaRankerServer.Modules.Media.Contracts;

public class MediaCollectionDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CollectionType { get; set; } = string.Empty;
    public long MediaTypeId { get; set; }
    public string MediaTypeName { get; set; } = string.Empty;
    public long? ParentMediaCollectionId { get; set; }
    public string? ParentMediaCollectionTitle { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CoverImageUrl { get; set; }
}

public static class MediaCollectionDtoMapper
{
    public static MediaCollectionDto Map(MediaCollection collection, IFileService fileService)
    {
        string? coverImageUrl = null;
        if (collection.CoverFileKey != null)
        {
            coverImageUrl = fileService.GetFileUrl(collection.CoverFileKey, FileEntityType.MediaCover);
        }

        return new MediaCollectionDto
        {
            Id = collection.Id,
            Title = collection.Title,
            CollectionType = collection.CollectionType.ToString(),
            MediaTypeId = collection.MediaTypeId,
            MediaTypeName = collection.MediaType.Name,
            ParentMediaCollectionId = collection.ParentMediaCollectionId,
            ParentMediaCollectionTitle = collection.ParentMediaCollection?.Title,
            ReleaseDate = collection.ReleaseDate,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt,
            CoverImageUrl = coverImageUrl
        };
    }
}
