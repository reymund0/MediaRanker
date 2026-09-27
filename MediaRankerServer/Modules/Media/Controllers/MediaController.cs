using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Extensions;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaController(IMediaService mediaService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMedia([FromQuery] string? mediaType, [FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        var media = await mediaService.GetAllMediaAsync(mediaType, request, cancellationToken);
        return Ok(media);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertMedia([FromBody] MediaUpsertRequest request, CancellationToken cancellationToken)
    {
        MediaDto media;
        var userId = User.GetAuthenticatedUserId();

        if (request.Id is null)
        {
            media = await mediaService.CreateMediaAsync(userId, request, cancellationToken);
        }
        else
        {
            media = await mediaService.UpdateMediaAsync(userId, request.Id.Value, request, cancellationToken);
        }

        return Ok(media);
    }

    // Tombstones keep removed upload URLs at 404 instead of ASP.NET's method-matching 405.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("UploadCover")]
    [HttpPost("CompleteUploadCover/{uploadId:long}")]
    public IActionResult RetiredCoverUpload() => new ObjectResult(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Type = "about:blank",
        Title = "Not Found",
        Detail = "This endpoint is no longer available."
    }) { StatusCode = StatusCodes.Status404NotFound };

    [HttpDelete("{mediaId:long}")]
    public async Task<IActionResult> DeleteMedia(long mediaId, CancellationToken cancellationToken)
    {
        await mediaService.DeleteMediaAsync(mediaId, cancellationToken);
        return Ok(true);
    }
}
