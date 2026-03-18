using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaController(IMediaService mediaService, IMediaCoverService mediaCoverService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMedia(CancellationToken cancellationToken)
    {
        var media = await mediaService.GetAllMediaAsync(cancellationToken);
        return Ok(media);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertMedia([FromBody] MediaUpsertRequest request, CancellationToken cancellationToken)
    {
        MediaDto media;

        if (request.Id is null)
        {
            media = await mediaService.CreateMediaAsync(request, cancellationToken);
        }
        else
        {
            media = await mediaService.UpdateMediaAsync(request.Id.Value, request, cancellationToken);
        }

        return Ok(media);
    }

    [HttpPost("UploadCover")]
    public async Task<IActionResult> GenerateUploadCoverUrl([FromBody] GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken)
    {
        var url = await mediaCoverService.GenerateUploadCoverUrlAsync(request, cancellationToken);
        return Ok(url);
    }

    [HttpPost("CompleteUploadCover")]
    public async Task<IActionResult> CompleteUploadCover([FromBody] CompleteUploadCoverRequest request, CancellationToken cancellationToken)
    {
        await mediaCoverService.CompleteUploadCoverAsync(request, cancellationToken);
        return Ok(true);
    }

    [HttpDelete("{mediaId:long}")]
    public async Task<IActionResult> DeleteMedia(long mediaId, CancellationToken cancellationToken)
    {
        await mediaService.DeleteMediaAsync(mediaId, cancellationToken);
        return Ok(true);
    }
}
