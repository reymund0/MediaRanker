using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services;

public interface IMediaCoverService
{
  Task<GenerateUploadCoverUrlResponse> GenerateUploadCoverUrlAsync(GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken);
  Task CompleteUploadCoverAsync(CompleteUploadCoverRequest request, CancellationToken cancellationToken);
}
