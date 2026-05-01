using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Extensions;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaController(IMediaService mediaService, IMediaCoverService mediaCoverService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMedia([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        var media = await mediaService.GetAllMediaAsync(request, cancellationToken);
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

    [HttpPost("UploadCover")]
    public async Task<IActionResult> GenerateUploadCoverUrl([FromBody] GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        var url = await mediaCoverService.GenerateUploadCoverUrlAsync(userId, request, cancellationToken);
        return Ok(url);
    }

    [HttpPost("CompleteUploadCover/{uploadId:long}")]
    public async Task<IActionResult> CompleteUploadCover(long uploadId, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        await mediaCoverService.CompleteUploadCoverAsync(userId, uploadId, cancellationToken);
        return Ok(true);
    }

    [HttpDelete("{mediaId:long}")]
    public async Task<IActionResult> DeleteMedia(long mediaId, CancellationToken cancellationToken)
    {
        await mediaService.DeleteMediaAsync(mediaId, cancellationToken);
        return Ok(true);
    }
}
