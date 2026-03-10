using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaController(IMediaService mediaService) : ControllerBase
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

    [HttpDelete("{mediaId:long}")]
    public async Task<IActionResult> DeleteMedia(long mediaId, CancellationToken cancellationToken)
    {
        await mediaService.DeleteMediaAsync(mediaId, cancellationToken);
        return Ok(true);
    }
}
