using MediaRankerServer.Modules.Rankings.Contracts;
namespace MediaRankerServer.Modules.Rankings.Services;

public interface IRankedMediaService
{
  Task<List<RankedMediaDto>> GetRankedMediaAsync(string userId, CancellationToken cancellationToken = default);
  Task<List<UnreviewedMediaDto>> GetUnreviewedMediaByTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default);
  Task<RankedMediaDto> CreateRankedMediaAsync(string userId, RankedMediaUpsertRequest request, CancellationToken cancellationToken = default);
  Task<RankedMediaDto> UpdateRankedMediaAsync(string userId, long rankedMediaId, RankedMediaUpsertRequest request, CancellationToken cancellationToken = default);
  Task DeleteRankedMediaAsync(string userId, long rankedMediaId, CancellationToken cancellationToken = default);
    
}