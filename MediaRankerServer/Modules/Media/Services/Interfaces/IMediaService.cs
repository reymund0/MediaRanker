using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Shared.Paging;

namespace MediaRankerServer.Modules.Media.Services.Interfaces;

public interface IMediaService
{
    Task<List<MediaTypeDto>> GetMediaTypesAsync(CancellationToken cancellationToken = default);
    Task<MediaTypeDto?> GetMediaTypeByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PageResult<MediaDto>> GetAllMediaAsync(PageRequest request, CancellationToken cancellationToken = default);
    Task<MediaDto?> GetMediaByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<MediaDto> CreateMediaAsync(string userId, MediaUpsertRequest request, CancellationToken cancellationToken = default);
    Task<MediaDto> UpdateMediaAsync(string userId, long mediaId, MediaUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteMediaAsync(long mediaId, CancellationToken cancellationToken = default);
}