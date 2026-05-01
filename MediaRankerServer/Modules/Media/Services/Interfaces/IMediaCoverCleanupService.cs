namespace MediaRankerServer.Modules.Media.Services.Interfaces;

public interface IMediaCoverCleanupService
{
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
