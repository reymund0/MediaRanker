using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services;

public interface IMediaService
{
    Task<List<MediaTypeDto>> GetMediaTypesAsync(CancellationToken cancellationToken = default);
}