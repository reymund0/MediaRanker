using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Extensions;
using MediaRankerServer.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.Modules.Media.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MediaCollectionController(IMediaCollectionService mediaCollectionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCollections([FromQuery] PageRequest request, [FromQuery] string? mediaType, [FromQuery] string? collectionType, [FromQuery] long? parentId, CancellationToken cancellationToken)
    {
        var collections = await mediaCollectionService.GetAllCollectionsAsync(request, cancellationToken, mediaType, collectionType, parentId);
        return Ok(collections);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetCollectionById(long id, CancellationToken cancellationToken)
    {
        var collection = await mediaCollectionService.GetCollectionByIdAsync(id, cancellationToken);
        return collection is null ? NotFound() : Ok(collection);
    }

    [HttpGet("{id:long}/removal-counts")]
    public async Task<IActionResult> GetRemovalCounts(long id, CancellationToken cancellationToken)
        => Ok(await mediaCollectionService.GetSeriesRemovalCountsAsync(id, cancellationToken));

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
