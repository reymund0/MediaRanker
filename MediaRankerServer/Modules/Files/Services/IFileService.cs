using MediaRankerServer.Modules.Files.Contracts;

namespace MediaRankerServer.Modules.Files.Services;

public interface IFileService
{
  Task<StartUploadResponse> StartUploadAsync(StartUploadRequest request, CancellationToken cancellationToken = default);
  Task<FileDto> FinishUploadAsync(FinishUploadRequest request, CancellationToken cancellationToken = default);
  Task<FileDto> MarkUploadCopiedAsync(long uploadId, string userId, CancellationToken cancellationToken = default);
  Task<string> GetFileUrlAsync(string fileKey, CancellationToken cancellationToken = default);
}
