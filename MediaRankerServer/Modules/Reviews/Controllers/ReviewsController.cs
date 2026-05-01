using MediaRankerServer.Modules.Reviews.Services;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Shared.Extensions;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> GetUnreviewedMediaByType(long mediaTypeId, [FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      var unreviewedMedia = await reviewService.GetUnreviewedMediaByTypeAsync(userId, mediaTypeId, request, cancellationToken);
      return Ok(unreviewedMedia);
    }

    [HttpPost]
    public async Task<IActionResult> InsertReview([FromBody] ReviewInsertRequest request, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      var newReview = await reviewService.CreateReviewAsync(userId, request, cancellationToken);
      return Ok(newReview);
    }

    [HttpPatch("update")]
    public async Task<IActionResult> UpdateReview([FromBody] ReviewUpdateRequest request, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      var updatedReview = await reviewService.UpdateReviewAsync(userId, request.Id, request, cancellationToken);
      return Ok(updatedReview);
    }

    [HttpDelete("{reviewId}")]
    public async Task<IActionResult> DeleteReview(long reviewId, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      await reviewService.DeleteReviewAsync(userId, reviewId, cancellationToken);
      return Ok();
    }
}
