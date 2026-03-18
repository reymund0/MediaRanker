using MediaRankerServer.Modules.Media.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaTypesController(IMediaService mediaService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMediaTypes(CancellationToken cancellationToken)
    {
        var mediaTypes = await mediaService.GetMediaTypesAsync(cancellationToken);
        return Ok(mediaTypes);
    }
}