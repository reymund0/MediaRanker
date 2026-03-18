using MediaRankerServer.Modules.Media.Contracts;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaCoverService : IMediaCoverService
{
    public Task<GenerateUploadCoverUrlResponse> GenerateUploadCoverUrlAsync(GenerateUploadCoverUrlRequest request, CancellationToken cancellationToken)
    {
        // Validate that request is for images.
        // Create record in our new InProgressUploads table
        throw new NotImplementedException();
    }

    public Task CompleteUploadCoverAsync(CompleteUploadCoverRequest request, CancellationToken cancellationToken)
    {
        // Validate that request is for images.
        // Update record in our new InProgressUploads table
        // Copy file from temp location to final location
        // Update media record with cover URL
        throw new NotImplementedException();
    }
}
