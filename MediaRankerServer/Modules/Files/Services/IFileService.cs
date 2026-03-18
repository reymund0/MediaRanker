using MediaRankerServer.Modules.Files.Contracts;

namespace MediaRankerServer.Modules.Files.Services;

public interface IFileService
{
  Task<StartUploadResponse> StartUploadAsync(StartUploadRequest request, CancellationToken cancellationToken = default);
  Task<FinishUploadResponse> FinishUploadAsync(FinishUploadRequest request, CancellationToken cancellationToken = default);
  Task MarkUploadCompleteAsync(long uploadId, string userId, CancellationToken cancellationToken = default);
  Task<string> GetFileUrlAsync(string fileKey, CancellationToken cancellationToken = default);
  Task DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default);
}
