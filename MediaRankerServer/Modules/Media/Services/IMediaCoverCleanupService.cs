namespace MediaRankerServer.Modules.Media.Services;

public interface IMediaCoverCleanupService
{
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
