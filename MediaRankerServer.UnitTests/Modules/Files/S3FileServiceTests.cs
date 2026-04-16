using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using FluentValidation;
using MediaRankerServer.Modules.Files.Contracts;
using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Files;

public class S3FileServiceTests : IDisposable
{
    private readonly PostgreSQLContext _dbContext;
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly IValidator<StartUploadRequest> _startValidator;
    private readonly IValidator<FinishUploadRequest> _finishValidator;
    private readonly S3FileService _service;

    private const string DefaultUserId = "test-user-1";
    private const string DefaultBucket = "media-cover-bucket";

    public S3FileServiceTests()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PostgreSQLContext(options);

        _mockS3Client = new Mock<IAmazonS3>();
        _mockConfiguration = new Mock<IConfiguration>();

        _startValidator = new StartUploadRequestValidator();
        _finishValidator = new FinishUploadRequestValidator();

        // Setup bucket configuration
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns(DefaultBucket);
        _mockConfiguration.Setup(c => c.GetSection("AWS:S3:Buckets:MediaCover")).Returns(mockSection.Object);

        _service = new S3FileService(
            _mockS3Client.Object,
            _startValidator,
            _finishValidator,
            _dbContext,
            _mockConfiguration.Object
        );
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<FileUpload> SeedUploadAsync(FileUploadState state = FileUploadState.Uploading, string userId = DefaultUserId)
    {
        var upload = new FileUpload
        {
            UserId = userId,
            FileKey = Guid.NewGuid().ToString(),
            FileName = "test.png",
            EntityType = FileEntityType.MediaCover,
            ExpectedContentType = "image/png",
            ExpectedFileSizeBytes = 1024,
            State = state,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (state != FileUploadState.Uploading)
        {
            upload.ActualContentType = "image/png";
            upload.ActualFileSizeBytes = 1024;
        }

        _dbContext.FileUploads.Add(upload);
        await _dbContext.SaveChangesAsync();
        return upload;
    }

    [Fact]
    public async Task StartUploadAsync_HappyPath_ReturnsResponseAndPersistsRecord()
    {
        // Arrange
        var request = new StartUploadRequest
        {
            UserId = DefaultUserId,
            EntityType = FileEntityType.MediaCover.ToString(),
            FileName = "test.png",
            ContentType = "image/png",
            FileSizeBytes = 1024
        };

        _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://s3.amazonaws.com/upload-url");

        // Act
        var response = await _service.StartUploadAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.UploadUrl.Should().Be("https://s3.amazonaws.com/upload-url");
        
        var persisted = await _dbContext.FileUploads.FindAsync(response.UploadId);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(DefaultUserId);
        persisted.State.Should().Be(FileUploadState.Uploading);
        persisted.FileKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StartUploadAsync_WhenValidationFails_ThrowsDomainException()
    {
        // Arrange
        var request = new StartUploadRequest { UserId = "" }; // Invalid

        // Act
        var act = () => _service.StartUploadAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "file_validation_error");
    }

    [Fact]
    public async Task FinishUploadAsync_HappyPath_UpdatesStateAndReturnsDto()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Uploading);
        var request = new FinishUploadRequest
        {
            UploadId = upload.Id,
            UserId = DefaultUserId
        };

        var metadataResponse = (GetObjectMetadataResponse)Activator.CreateInstance(typeof(GetObjectMetadataResponse), true)!;
        typeof(AmazonWebServiceResponse).GetProperty("HttpStatusCode")?.SetValue(metadataResponse, System.Net.HttpStatusCode.OK);
        typeof(GetObjectMetadataResponse).GetProperty("ContentLength")?.SetValue(metadataResponse, 1024L);
        typeof(GetObjectMetadataResponse).GetProperty("ContentType")?.SetValue(metadataResponse, "image/png");

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadataResponse);

        // Act
        var result = await _service.FinishUploadAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.UploadId.Should().Be(upload.Id);
        result.ContentType.Should().Be("image/png");

        var updated = await _dbContext.FileUploads.FindAsync(upload.Id);
        updated!.State.Should().Be(FileUploadState.Uploaded);
        updated.ActualContentType.Should().Be("image/png");
        updated.ActualFileSizeBytes.Should().Be(1024);
    }

    [Fact]
    public async Task FinishUploadAsync_WhenUploadNotFound_ThrowsDomainException()
    {
        // Arrange
        var request = new FinishUploadRequest { UploadId = 999, UserId = DefaultUserId };

        // Act
        var act = () => _service.FinishUploadAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "upload_not_found");
    }

    [Fact]
    public async Task FinishUploadAsync_WhenUploadNotOwned_ThrowsDomainException()
    {
        // Arrange
        var upload = await SeedUploadAsync(userId: "other-user");
        var request = new FinishUploadRequest { UploadId = upload.Id, UserId = DefaultUserId };

        // Act
        var act = () => _service.FinishUploadAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "upload_not_owned");
    }

    [Fact]
    public async Task FinishUploadAsync_WhenInvalidState_ThrowsDomainException()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Uploaded);
        var request = new FinishUploadRequest { UploadId = upload.Id, UserId = DefaultUserId };

        // Act
        var act = () => _service.FinishUploadAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "upload_invalid_state");
    }

    [Fact]
    public async Task FinishUploadAsync_WhenS3MetadataFails_ThrowsDomainException()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Uploading);
        var request = new FinishUploadRequest { UploadId = upload.Id, UserId = DefaultUserId };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("S3 Error"));

        // Act
        var act = () => _service.FinishUploadAsync(request);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "s3_metadata_error");
    }

    [Fact]
    public async Task MarkUploadCopiedAsync_HappyPath_TransitionsState()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Uploaded);

        // Act
        await _service.MarkUploadCopiedAsync(upload.Id, DefaultUserId);

        // Assert
        var updated = await _dbContext.FileUploads.FindAsync(upload.Id);
        updated!.State.Should().Be(FileUploadState.Copied);
    }

    [Fact]
    public async Task MarkUploadCopiedAsync_WhenInvalidState_ThrowsDomainException()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Uploading);

        // Act
        var act = () => _service.MarkUploadCopiedAsync(upload.Id, DefaultUserId);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "upload_invalid_state");
    }

    [Fact]
    public async Task GetFileUrl_HappyPath_ReturnsUrl()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Copied);
        _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://s3.amazonaws.com/preview-url");

        // Act
        var url = _service.GetFileUrl(upload.FileKey, upload.EntityType);

        // Assert
        url.Should().Be("https://s3.amazonaws.com/preview-url");
    }

    [Fact]
    public async Task DeleteFileAsync_HappyPath_CallsS3AndDeleteRecord()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Copied);

        // Act
        await _service.DeleteFileAsync(upload.FileKey, upload.EntityType);

        // Assert
        _mockS3Client.Verify(s => s.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => r.Key == upload.FileKey && r.BucketName == DefaultBucket),
            It.IsAny<CancellationToken>()), Times.Once);

        var updated = await _dbContext.FileUploads.FindAsync(upload.Id);
        updated!.State.Should().Be(FileUploadState.Deleted);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenInvalidState_ThrowsDomainException()
    {
        // Arrange
        var upload = await SeedUploadAsync(FileUploadState.Uploading);

        // Act
        var act = () => _service.DeleteFileAsync(upload.FileKey, upload.EntityType);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "file_invalid_state");
    }
}
