using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services.Interfaces;

public interface IMediaCoverService
{
  Task<GenerateUploadCoverUrlResponse> GenerateUploadCoverUrlAsync(string userId, GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken);
  Task CompleteUploadCoverAsync(string userId, long uploadId, CancellationToken cancellationToken);
}
