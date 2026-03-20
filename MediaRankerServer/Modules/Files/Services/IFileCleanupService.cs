namespace MediaRankerServer.Modules.Files.Services;

/// <summary>
/// Internal interface for file cleanup operations, used by event handlers within the Files module.
/// This is not intended to be used by other modules.
/// </summary>
internal interface IFileCleanupService
{
    Task DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default);
}
