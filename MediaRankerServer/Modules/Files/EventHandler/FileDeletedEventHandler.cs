using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Shared.Exceptions;
using MediatR;

namespace MediaRankerServer.Modules.Files.EventHandler;

internal class FileDeletedEventHandler(IFileCleanupService cleanupService, ILogger<FileDeletedEventHandler> logger) 
    : INotificationHandler<FileDeletedEvent>
{
    public async Task Handle(FileDeletedEvent notification, CancellationToken cancellationToken)
    {
        try {
          await cleanupService.DeleteFileAsync(notification.FileKey, Enum.Parse<FileEntityType>(notification.EntityType), cancellationToken);
        }
        catch (DomainException ex)
        {
          // Check if file does not exists already.
          if (ex.Type == "file_not_found")
          {
            return;
          }
          else
          {
            logger.LogError(ex, "Failed to delete file {FileKey}", notification.FileKey);
          }
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Failed to delete file {FileKey}", notification.FileKey);
        }
    }
}
