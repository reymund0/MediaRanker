using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services;

public interface IMediaCoverService
{
  Task<GenerateUploadCoverUrlResponse> GenerateUploadCoverUrlAsync(string userId, GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken);
  Task CompleteUploadCoverAsync(string userId, long uploadId, CancellationToken cancellationToken);
  Task<FileDto> CopyCoverFileAsync(string userId, long uploadId, CancellationToken cancellationToken);
  Task DeleteCoverFileAsync(string fileKey, CancellationToken cancellationToken);
}
