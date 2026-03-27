using MediaRankerServer.Modules.Reviews.Contracts;
namespace MediaRankerServer.Modules.Reviews.Services;

public interface IReviewService
{
  Task<List<ReviewDto>> GetReviewsByMediaTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default);
  Task<List<UnreviewedMediaDto>> GetUnreviewedMediaByTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default);
  Task<ReviewDto> CreateReviewAsync(string userId, ReviewUpsertRequest request, CancellationToken cancellationToken = default);
  Task<ReviewDto> UpdateReviewAsync(string userId, long reviewId, ReviewUpsertRequest request, CancellationToken cancellationToken = default);
  Task DeleteReviewAsync(string userId, long reviewId, CancellationToken cancellationToken = default);
    
}