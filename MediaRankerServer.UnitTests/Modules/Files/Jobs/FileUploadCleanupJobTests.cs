using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Jobs;
using MediaRankerServer.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Files.Jobs;

public class FileUploadCleanupJobTests : IDisposable
{
    private readonly PostgreSQLContext _dbContext;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly TestFileUploadCleanupJob _job;

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

        _serviceProvider = new ServiceCollection()
            .AddSingleton(_dbContext)
            .AddSingleton(_mediatorMock.Object)
            .BuildServiceProvider();

        var jobOptions = Options.Create(new FileCleanupOptions
        {
            StaleDaysThreshold = 2
        });

        var loggerMock = new Mock<ILogger<FileUploadCleanupJob>>();

        _job = new TestFileUploadCleanupJob(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            jobOptions,
            loggerMock.Object);
    }

    [Fact]
    public async Task RunAsync_WhenUploadsAreStaleAndUploaded_PublishesDeleteEventsOnlyForMatchingRows()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await SeedUploadAsync("stale-uploaded", FileUploadState.Uploaded, now.AddDays(-7));
        await SeedUploadAsync("fresh-uploaded", FileUploadState.Uploaded, now.AddHours(-12));
        await SeedUploadAsync("stale-uploading", FileUploadState.Uploading, now.AddDays(-7));

        // Act
        await _job.RunOnceForTestAsync(_serviceProvider, CancellationToken.None);

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
        var now = DateTimeOffset.UtcNow;
        await SeedUploadAsync("fail-upload", FileUploadState.Uploaded, now.AddDays(-7));
        await SeedUploadAsync("success-upload", FileUploadState.Uploaded, now.AddDays(-7));

        _mediatorMock
            .Setup(m => m.Publish(
                It.Is<FileDeletedEvent>(e => e.FileKey == "fail-upload"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test publish failure"));

        // Act
        await _job.RunOnceForTestAsync(_serviceProvider, CancellationToken.None);

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
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TestFileUploadCleanupJob(
        IServiceScopeFactory scopeFactory,
        IOptions<FileCleanupOptions> options,
        ILogger<FileUploadCleanupJob> logger)
        : FileUploadCleanupJob(scopeFactory, options, logger)
    {
        public Task RunOnceForTestAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken) =>
            RunJobAsync(serviceProvider, cancellationToken);
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
