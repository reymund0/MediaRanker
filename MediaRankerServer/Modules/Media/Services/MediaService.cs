using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaService(PostgreSQLContext dbContext) : IMediaService
{
    public Task<List<MediaTypeDto>> GetMediaTypesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.MediaTypes
            .Select(mt => MediaTypeMapper.Map(mt))
            .ToListAsync(cancellationToken);
    }
}
