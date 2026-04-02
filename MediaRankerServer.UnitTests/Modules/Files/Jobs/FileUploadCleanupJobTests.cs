using FluentAssertions;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Jobs;
using MediaRankerServer.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Files.Jobs;

public class FileUploadCleanupJobTests : IDisposable
{
    private readonly PostgreSQLContext _dbContext;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly FileUploadCleanupRunner _runner;

    public FileUploadCleanupJobTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        _dbContext = new PostgreSQLContext(options);

        _mediatorMock = new Mock<IMediator>();
        _mediatorMock
            .Setup(m => m.Publish(It.IsAny<FileDeletedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<FileUploadCleanupRunner>>();

        _runner = new FileUploadCleanupRunner(
            _dbContext,
            _mediatorMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task RunAsync_WhenUploadsAreStaleAndUploaded_PublishesDeleteEventsOnlyForMatchingRows()
    {
        // Arrange
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await SeedUploadAsync("stale-uploaded", FileUploadState.Uploaded, cutoff.AddHours(-1));
        await SeedUploadAsync("fresh-uploaded", FileUploadState.Uploaded, cutoff.AddHours(1));
        await SeedUploadAsync("stale-uploading", FileUploadState.Uploading, cutoff.AddHours(-1));

        // Act
        await _runner.RunAsync(cutoff, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<FileDeletedEvent>(e => e.FileKey == "stale-uploaded" && e.EntityType == FileEntityType.MediaCover.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<FileDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenPublishFailsForOneUpload_ContinuesPublishingRemainingUploads()
    {
        // Arrange
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await SeedUploadAsync("fail-upload", FileUploadState.Uploaded, cutoff.AddHours(-1));
        await SeedUploadAsync("success-upload", FileUploadState.Uploaded, cutoff.AddHours(-1));

        _mediatorMock
            .Setup(m => m.Publish(
                It.Is<FileDeletedEvent>(e => e.FileKey == "fail-upload"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test publish failure"));

        // Act
        await _runner.RunAsync(cutoff, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<FileDeletedEvent>(e => e.FileKey == "fail-upload"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<FileDeletedEvent>(e => e.FileKey == "success-upload"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<FileDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedUploadAsync(string fileKey, FileUploadState state, DateTimeOffset updatedAt)
    {
        _dbContext.FileUploads.Add(new FileUpload
        {
            UserId = "test-user-1",
            EntityType = FileEntityType.MediaCover,
            FileKey = fileKey,
            FileName = "test.png",
            ExpectedContentType = "image/png",
            ExpectedFileSizeBytes = 1024,
            State = state,
            CreatedAt = updatedAt.AddMinutes(-1),
            UpdatedAt = updatedAt,
        });

        await _dbContext.SaveChangesAsync();
    }
}
