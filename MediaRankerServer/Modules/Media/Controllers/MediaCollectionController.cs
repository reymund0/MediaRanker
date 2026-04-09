using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaCollectionController(IMediaCollectionService mediaCollectionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCollections(CancellationToken cancellationToken)
    {
        var collections = await mediaCollectionService.GetAllCollectionsAsync(cancellationToken);
        return Ok(collections);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertCollection([FromBody] MediaCollectionUpsertRequest request, CancellationToken cancellationToken)
    {
        MediaCollectionDto collection;
        var userId = User.GetAuthenticatedUserId();

        if (request.Id is null)
        {
            collection = await mediaCollectionService.CreateCollectionAsync(userId, request, cancellationToken);
        }
        else
        {
            collection = await mediaCollectionService.UpdateCollectionAsync(userId, request.Id.Value, request, cancellationToken);
        }

        return Ok(collection);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteCollection(long id, CancellationToken cancellationToken)
    {
        await mediaCollectionService.DeleteCollectionAsync(id, cancellationToken);
        return Ok(true);
    }
}
