using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Shared.Paging;
namespace MediaRankerServer.Modules.Reviews.Services;

public interface IReviewService
{
  Task<List<ReviewDto>> GetReviewsByMediaTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default);
  Task<PageResult<UnreviewedMediaDto>> GetUnreviewedMediaByTypeAsync(string userId, long mediaTypeId, PageRequest request, CancellationToken cancellationToken = default);
  Task<ReviewDto> CreateReviewAsync(string userId, ReviewInsertRequest request, CancellationToken cancellationToken = default);
  Task<ReviewDto> UpdateReviewAsync(string userId, long reviewId, ReviewUpdateRequest request, CancellationToken cancellationToken = default);
  Task DeleteReviewAsync(string userId, long reviewId, CancellationToken cancellationToken = default);
    
}