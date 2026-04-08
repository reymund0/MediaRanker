using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services;

public interface IMediaCollectionService
{
    Task<List<MediaCollectionDto>> GetAllCollectionsAsync(CancellationToken cancellationToken = default);
    Task<MediaCollectionDto?> GetCollectionByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<MediaCollectionDto> CreateCollectionAsync(string userId, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default);
    Task<MediaCollectionDto> UpdateCollectionAsync(string userId, long id, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(long id, CancellationToken cancellationToken = default);
}
