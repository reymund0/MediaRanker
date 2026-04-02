using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Data.Entities;

namespace MediaRankerServer.Modules.Files.Services;

public interface IFileService
{
  Task<StartUploadResponse> StartUploadAsync(StartUploadRequest request, CancellationToken cancellationToken = default);
  Task<FileDto> FinishUploadAsync(FinishUploadRequest request, CancellationToken cancellationToken = default);
  Task<FileDto> MarkUploadCopiedAsync(long uploadId, string userId, CancellationToken cancellationToken = default);
  string GetFileUrl(string fileKey, FileEntityType entityType);
}
