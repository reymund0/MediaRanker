using MediaRankerServer.Modules.Rankings.Services;
using MediaRankerServer.Modules.Rankings.Contracts;
using Microsoft.AspNetCore.Mvc;
using MediaRankerServer.Shared.Extensions;

namespace MediaRankerServer.Modules.Rankings.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RankedMediaController(IRankedMediaService rankedMediaService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRankedMedia(CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      var rankedMedia = await rankedMediaService.GetRankedMediaAsync(userId, cancellationToken);
      return Ok(rankedMedia);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertRankedMedia([FromBody] RankedMediaUpsertRequest request, CancellationToken cancellationToken)
    {
      RankedMediaDto updatedRankedMedia;

      var userId = User.GetAuthenticatedUserId();
      if (request.Id is null)
      {
        updatedRankedMedia = await rankedMediaService.CreateRankedMediaAsync(userId, request, cancellationToken);
      } else {
        updatedRankedMedia = await rankedMediaService.UpdateRankedMediaAsync(userId, request.Id.Value, request, cancellationToken);
      }

      return Ok(updatedRankedMedia);
    }

    [HttpDelete("{rankedMediaId}")]
    public async Task<IActionResult> DeleteRankedMedia(long rankedMediaId, CancellationToken cancellationToken)
    {
      var userId = User.GetAuthenticatedUserId();
      await rankedMediaService.DeleteRankedMediaAsync(userId, rankedMediaId, cancellationToken);
      return Ok();
    }
}
