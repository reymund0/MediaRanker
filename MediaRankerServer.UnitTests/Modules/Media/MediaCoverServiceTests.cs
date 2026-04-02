using FluentValidation;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediatR;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class MediaCoverServiceTests : IDisposable
{
    private readonly PostgreSQLContext _dbContext;
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IValidator<GenerateUploadCoverUrlRequest>> _mockValidator;
    private readonly MediaCoverService _service;

    private const string DefaultUserId = "test-user-1";

    public MediaCoverServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PostgreSQLContext(options);

        _mockFileService = new Mock<IFileService>();
        _mockMediator = new Mock<IMediator>();
        _mockValidator = new Mock<IValidator<GenerateUploadCoverUrlRequest>>();

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<GenerateUploadCoverUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _service = new MediaCoverService(
            _mockFileService.Object,
            _dbContext,
            _mockMediator.Object,
            _mockValidator.Object
        );
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenerateUploadCoverUrlAsync_NoMediaId_ReturnsUrlAndUploadId()
    {
        // Arrange
        var request = new GenerateUploadCoverUrlRequest
        {
            FileName = "cover.png",
            ContentType = "image/png",
            FileSizeBytes = 1024
        };

        _mockFileService.Setup(f => f.StartUploadAsync(It.IsAny<StartUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartUploadResponse { UploadId = 1, UploadUrl = "http://presigned-url" });

        // Act
        var result = await _service.GenerateUploadCoverUrlAsync(DefaultUserId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be("http://presigned-url");
        result.UploadId.Should().Be(1);

        _mockFileService.Verify(f => f.StartUploadAsync(It.Is<StartUploadRequest>(r => 
            r.UserId == DefaultUserId && 
            r.EntityType == FileEntityType.MediaCover.ToString() &&
            r.FileName == request.FileName &&
            r.ContentType == request.ContentType &&
            r.FileSizeBytes == request.FileSizeBytes
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateUploadCoverUrlAsync_WithValidMediaId_ReturnsUrlAndUploadId()
    {
        // Arrange
        var media = new MediaEntity { Id = 123, Title = "Test Movie", MediaTypeId = 1 };
        _dbContext.Media.Add(media);
        await _dbContext.SaveChangesAsync();

        var request = new GenerateUploadCoverUrlRequest
        {
            MediaId = 123,
            FileName = "cover.png",
            ContentType = "image/png",
            FileSizeBytes = 1024
        };

        _mockFileService.Setup(f => f.StartUploadAsync(It.IsAny<StartUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartUploadResponse { UploadId = 1, UploadUrl = "http://presigned-url" });

        // Act
        var result = await _service.GenerateUploadCoverUrlAsync(DefaultUserId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UploadId.Should().Be(1);
        
        _mockFileService.Verify(f => f.StartUploadAsync(It.Is<StartUploadRequest>(r => r.EntityId == 123), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateUploadCoverUrlAsync_WhenMediaNotFound_ThrowsDomainException()
    {
        // Arrange
        var request = new GenerateUploadCoverUrlRequest { MediaId = 999 };

        // Act
        var act = () => _service.GenerateUploadCoverUrlAsync(DefaultUserId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "media_not_found");
    }

    [Fact]
    public async Task CompleteUploadCoverAsync_HappyPath_CompletesWithoutException()
    {
        // Arrange
        var uploadedFile = new FileDto
        {
            UploadId = 1,
            UserId = DefaultUserId,
            ContentType = "image/png",
            FileKey = "key1",
            FileName = "cover.png"
        };

        _mockFileService.Setup(f => f.FinishUploadAsync(It.IsAny<FinishUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        // Act
        var act = () => _service.CompleteUploadCoverAsync(DefaultUserId, 1, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMediator.Verify(m => m.Publish(It.IsAny<FileDeletedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteUploadCoverAsync_WhenInvalidFileType_ThrowsDomainExceptionAndPublishesDelete()
    {
        // Arrange
        var uploadedFile = new FileDto
        {
            UploadId = 1,
            UserId = DefaultUserId,
            ContentType = "application/pdf",
            FileKey = "key1",
            FileName = "file.pdf"
        };

        _mockFileService.Setup(f => f.FinishUploadAsync(It.IsAny<FinishUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        // Act
        var act = () => _service.CompleteUploadCoverAsync(DefaultUserId, 1, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "invalid_file_type");

        _mockMediator.Verify(m => m.Publish(
            It.Is<FileDeletedEvent>(e => e.FileKey == "key1" && e.EntityType == FileEntityType.MediaCover.ToString()), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CopyCoverFileAsync_CallsFileServiceAndReturnsFileDto()
    {
        // Arrange
        var fileDto = new FileDto { UploadId = 1, FileKey = "key1" };
        _mockFileService.Setup(f => f.MarkUploadCopiedAsync(1, DefaultUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDto);

        // Act
        var result = await _service.CopyCoverFileAsync(DefaultUserId, 1, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(fileDto);
        _mockFileService.Verify(f => f.MarkUploadCopiedAsync(1, DefaultUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCoverFileAsync_PublishesFileDeletedEvent()
    {
        // Act
        await _service.DeleteCoverFileAsync("123", CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Publish(
            It.Is<FileDeletedEvent>(e => e.FileKey == "123" && e.EntityType == FileEntityType.MediaCover.ToString()), 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
