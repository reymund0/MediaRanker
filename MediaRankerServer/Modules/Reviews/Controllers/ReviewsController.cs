using MediaRankerServer.Modules.Reviews.Services;
using MediaRankerServer.Modules.Reviews.Contracts;
using Microsoft.AspNetCore.Mvc;
using MediaRankerServer.Shared.Extensions;

namespace MediaRankerServer.Modules.Reviews.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpGet("byMediaType/{mediaTypeId:long}")]
    public async Task<IActionResult> GetReviewsByMediaType(long mediaTypeId, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      var reviews = await reviewService.GetReviewsByMediaTypeAsync(userId, mediaTypeId, cancellationToken);
      return Ok(reviews);
    }

    [HttpGet("unreviewedByType")]
    public async Task<IActionResult> GetUnreviewedMediaByType(long mediaTypeId, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      var unreviewedMedia = await reviewService.GetUnreviewedMediaByTypeAsync(userId, mediaTypeId, cancellationToken);
      return Ok(unreviewedMedia);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertReviews([FromBody] ReviewUpsertRequest request, CancellationToken cancellationToken)
    {
      ReviewDto updatedReviews;

      var userId = User.GetAuthenticatedUserId();
      if (request.Id is null)
      {
        updatedReviews = await reviewService.CreateReviewAsync(userId, request, cancellationToken);
      } else {
        updatedReviews = await reviewService.UpdateReviewAsync(userId, request.Id.Value, request, cancellationToken);
      }

      return Ok(updatedReviews);
    }

    [HttpDelete("{reviewId}")]
    public async Task<IActionResult> DeleteReviews(long reviewId, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      await reviewService.DeleteReviewAsync(userId, reviewId, cancellationToken);
      return Ok();
    }
}
