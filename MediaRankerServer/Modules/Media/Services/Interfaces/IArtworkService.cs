using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services.Interfaces;

public interface IArtworkService
{
    Task<IReadOnlyDictionary<long, CoverPresentation>> GetMediaArtworkAsync(IEnumerable<long> mediaIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, CoverPresentation>> GetCollectionArtworkAsync(IEnumerable<long> collectionIds, CancellationToken ct = default);
}
