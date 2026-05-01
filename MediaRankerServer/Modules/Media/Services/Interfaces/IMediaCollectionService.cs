using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Shared.Paging;

namespace MediaRankerServer.Modules.Media.Services.Interfaces;

public interface IMediaCollectionService
{
    Task<PageResult<MediaCollectionDto>> GetAllCollectionsAsync(PageRequest request, CancellationToken cancellationToken = default);
    Task<MediaCollectionDto?> GetCollectionByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<MediaCollectionDto> CreateCollectionAsync(string userId, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default);
    Task<MediaCollectionDto> UpdateCollectionAsync(string userId, long id, MediaCollectionUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(long id, CancellationToken cancellationToken = default);
}
